using PlantOps.Domain.WorkOrders;

namespace PlantOps.Domain.UnitTests.WorkOrders;

public sealed class WorkOrderApproveTests
{
    [Fact]
    public void Approve_moves_an_open_work_order_to_Approved_and_records_the_supervisor_and_time()
    {
        var workOrder = TestWorkOrders.Open();

        var result = workOrder.Approve(TestWorkOrders.SupervisorId, TestWorkOrders.ApprovedAt);

        result.IsSuccess.Should().BeTrue();
        workOrder.Status.Should().Be(WorkOrderStatus.Approved);
        workOrder.ApprovedByUserId.Should().Be(TestWorkOrders.SupervisorId);
        workOrder.ApprovedAtUtc.Should().Be(TestWorkOrders.ApprovedAt);
        workOrder.AssignedToUserId.Should().BeNull();
    }
}
