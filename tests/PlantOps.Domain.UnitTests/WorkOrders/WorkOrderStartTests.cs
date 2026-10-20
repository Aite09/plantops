using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Domain.UnitTests.WorkOrders;

public sealed class WorkOrderStartTests
{
    [Fact]
    public void Start_by_the_assigned_technician_moves_to_InProgress_and_records_the_start_time()
    {
        var workOrder = TestWorkOrders.Assigned();

        var result = workOrder.Start(TestWorkOrders.TechnicianId, TestWorkOrders.StartedAt);

        result.IsSuccess.Should().BeTrue();
        workOrder.Status.Should().Be(WorkOrderStatus.InProgress);
        workOrder.StartedAtUtc.Should().Be(TestWorkOrders.StartedAt);
        workOrder.AssignedToUserId.Should().Be(TestWorkOrders.TechnicianId);
    }

    [Fact]
    public void Start_by_a_technician_who_is_not_assigned_is_forbidden()
    {
        var workOrder = TestWorkOrders.Assigned();

        var result = workOrder.Start(TestWorkOrders.OtherTechnicianId, TestWorkOrders.StartedAt);

        result.Error.Should().Be(WorkOrderErrors.NotAssignedTechnician);
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        workOrder.Status.Should().Be(WorkOrderStatus.Assigned);
        workOrder.StartedAtUtc.Should().BeNull();
    }
}
