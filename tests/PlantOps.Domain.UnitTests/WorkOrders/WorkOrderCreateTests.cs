using PlantOps.Domain.Plant;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Domain.UnitTests.WorkOrders;

public sealed class WorkOrderCreateTests
{
    private const string ValidTitle = "Seal leak on P-101A";
    private const string ValidDescription = "Mechanical seal leaking at about one drop per second.";

    private static readonly Equipment Pump = TestWorkOrders.Pump();

    [Fact]
    public void Valid_report_starts_Open_with_the_trimmed_details_reporter_and_time()
    {
        var result = WorkOrder.Create(
            "  Seal leak on P-101A  ",
            "\tMechanical seal leaking at about one drop per second.\n",
            WorkOrderPriority.Critical,
            Pump,
            TestWorkOrders.ReporterId,
            TestWorkOrders.ReportedAt);

        result.IsSuccess.Should().BeTrue();
        var workOrder = result.Value;
        workOrder.Id.Should().NotBeEmpty();
        workOrder.Status.Should().Be(WorkOrderStatus.Open);
        workOrder.Title.Should().Be("Seal leak on P-101A");
        workOrder.Description.Should().Be("Mechanical seal leaking at about one drop per second.");
        workOrder.Priority.Should().Be(WorkOrderPriority.Critical);
        workOrder.ReportedByUserId.Should().Be(TestWorkOrders.ReporterId);
        workOrder.CreatedAtUtc.Should().Be(TestWorkOrders.ReportedAt);
    }

    [Fact]
    public void Plant_unit_and_equipment_ids_are_taken_from_the_equipment()
    {
        var equipment = new Equipment(
            new Guid("b4c2e9f0-0000-4000-8000-000000000204"),
            new Guid("b4c2e9f0-0000-4000-8000-000000000200"),
            "E-204",
            "Overhead condenser");

        var workOrder = WorkOrder.Create(
            ValidTitle, ValidDescription, WorkOrderPriority.Medium, equipment, TestWorkOrders.ReporterId, TestWorkOrders.ReportedAt).Value;

        workOrder.EquipmentId.Should().Be(equipment.Id);
        workOrder.PlantUnitId.Should().Be(equipment.PlantUnitId);
    }

    [Fact]
    public void A_new_work_order_has_no_approval_assignment_or_closure_yet()
    {
        var workOrder = WorkOrder.Create(
            ValidTitle, ValidDescription, WorkOrderPriority.Low, Pump, TestWorkOrders.ReporterId, TestWorkOrders.ReportedAt).Value;

        workOrder.ApprovedByUserId.Should().BeNull();
        workOrder.ApprovedAtUtc.Should().BeNull();
        workOrder.AssignedToUserId.Should().BeNull();
        workOrder.AssignedAtUtc.Should().BeNull();
        workOrder.StartedAtUtc.Should().BeNull();
        workOrder.ClosedAtUtc.Should().BeNull();
        workOrder.ResolutionNotes.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Blank_title_is_rejected(string? title)
    {
        // Callers outside a nullable context can still pass null, so the domain guards against it too.
        var result = WorkOrder.Create(
            title!, ValidDescription, WorkOrderPriority.High, Pump, TestWorkOrders.ReporterId, TestWorkOrders.ReportedAt);

        result.Error.Should().Be(WorkOrderErrors.TitleRequired);
    }

    [Fact]
    public void Title_longer_than_the_maximum_is_rejected()
    {
        var title = new string('T', WorkOrder.TitleMaxLength + 1);

        var result = WorkOrder.Create(
            title, ValidDescription, WorkOrderPriority.High, Pump, TestWorkOrders.ReporterId, TestWorkOrders.ReportedAt);

        result.Error.Should().Be(WorkOrderErrors.TitleRequired);
    }

    [Fact]
    public void Title_of_exactly_the_maximum_length_is_accepted_and_surrounding_spaces_do_not_count()
    {
        var title = new string('T', WorkOrder.TitleMaxLength);

        var result = WorkOrder.Create(
            $"  {title}  ", ValidDescription, WorkOrderPriority.High, Pump, TestWorkOrders.ReporterId, TestWorkOrders.ReportedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be(title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Blank_description_is_rejected(string? description)
    {
        var result = WorkOrder.Create(
            ValidTitle, description!, WorkOrderPriority.High, Pump, TestWorkOrders.ReporterId, TestWorkOrders.ReportedAt);

        result.Error.Should().Be(WorkOrderErrors.DescriptionRequired);
    }

    [Fact]
    public void Description_longer_than_the_maximum_is_rejected()
    {
        var description = new string('D', WorkOrder.DescriptionMaxLength + 1);

        var result = WorkOrder.Create(
            ValidTitle, description, WorkOrderPriority.High, Pump, TestWorkOrders.ReporterId, TestWorkOrders.ReportedAt);

        result.Error.Should().Be(WorkOrderErrors.DescriptionRequired);
    }

    [Fact]
    public void Description_of_exactly_the_maximum_length_is_accepted_and_surrounding_spaces_do_not_count()
    {
        var description = new string('D', WorkOrder.DescriptionMaxLength);

        var result = WorkOrder.Create(
            ValidTitle, $" {description}\n", WorkOrderPriority.High, Pump, TestWorkOrders.ReporterId, TestWorkOrders.ReportedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be(description);
    }

    [Theory]
    [InlineData((WorkOrderPriority)0)]
    [InlineData((WorkOrderPriority)5)]
    [InlineData((WorkOrderPriority)(-1))]
    public void Undefined_priority_is_rejected(WorkOrderPriority priority)
    {
        var result = WorkOrder.Create(
            ValidTitle, ValidDescription, priority, Pump, TestWorkOrders.ReporterId, TestWorkOrders.ReportedAt);

        result.Error.Should().Be(WorkOrderErrors.InvalidPriority);
    }

    [Fact]
    public void Empty_reporter_id_is_rejected()
    {
        var result = WorkOrder.Create(
            ValidTitle, ValidDescription, WorkOrderPriority.High, Pump, Guid.Empty, TestWorkOrders.ReportedAt);

        result.Error.Should().Be(WorkOrderErrors.UserRequired);
    }
}
