using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Domain.UnitTests.WorkOrders;

public sealed class WorkOrderUpdateDetailsTests
{
    private const string NewTitle = "Seal leak on P-101A getting worse";
    private const string NewDescription = "Leak now a steady stream; pump still running.";

    // One example per rule: the rules themselves are covered in depth by WorkOrderCreateTests, which shares them.
    public static readonly TheoryData<string, string, WorkOrderPriority, string> InvalidDetails = new()
    {
        { "   ", NewDescription, WorkOrderPriority.Critical, "WorkOrder.TitleRequired" },
        { new string('T', WorkOrder.TitleMaxLength + 1), NewDescription, WorkOrderPriority.Critical, "WorkOrder.TitleRequired" },
        { NewTitle, "", WorkOrderPriority.Critical, "WorkOrder.DescriptionRequired" },
        { NewTitle, new string('D', WorkOrder.DescriptionMaxLength + 1), WorkOrderPriority.Critical, "WorkOrder.DescriptionRequired" },
        { NewTitle, NewDescription, (WorkOrderPriority)99, "WorkOrder.InvalidPriority" },
    };

    [Theory]
    [InlineData(WorkOrderStatus.Open)]
    [InlineData(WorkOrderStatus.Approved)]
    [InlineData(WorkOrderStatus.Assigned)]
    [InlineData(WorkOrderStatus.InProgress)]
    public void Details_can_be_edited_in_any_status_before_Closed(WorkOrderStatus status)
    {
        var workOrder = TestWorkOrders.InStatus(status);

        var result = workOrder.UpdateDetails($"  {NewTitle} ", $"{NewDescription}\n", WorkOrderPriority.Critical);

        result.IsSuccess.Should().BeTrue();
        workOrder.Title.Should().Be(NewTitle);
        workOrder.Description.Should().Be(NewDescription);
        workOrder.Priority.Should().Be(WorkOrderPriority.Critical);
        workOrder.Status.Should().Be(status);
    }

    [Fact]
    public void A_closed_work_order_cannot_be_edited()
    {
        var workOrder = TestWorkOrders.Closed();

        var result = workOrder.UpdateDetails(NewTitle, NewDescription, WorkOrderPriority.Critical);

        result.Error.Should().Be(WorkOrderErrors.ClosedCannotBeEdited);
        result.Error.Type.Should().Be(ErrorType.Conflict);
        workOrder.Should().BeEquivalentTo(TestWorkOrders.Closed(), options => options.Excluding(w => w.Id));
    }

    [Fact]
    public void A_closed_work_order_reports_that_it_is_closed_even_when_the_new_details_are_invalid()
    {
        var workOrder = TestWorkOrders.Closed();

        var result = workOrder.UpdateDetails("", "", (WorkOrderPriority)99);

        result.Error.Should().Be(WorkOrderErrors.ClosedCannotBeEdited);
    }

    [Theory]
    [MemberData(nameof(InvalidDetails))]
    public void Invalid_details_are_rejected_and_nothing_changes(
        string title, string description, WorkOrderPriority priority, string expectedErrorCode)
    {
        var workOrder = TestWorkOrders.Open();

        var result = workOrder.UpdateDetails(title, description, priority);

        result.Error.Code.Should().Be(expectedErrorCode);
        workOrder.Should().BeEquivalentTo(TestWorkOrders.Open(), options => options.Excluding(w => w.Id));
    }
}
