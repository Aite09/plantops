using PlantOps.Domain.WorkOrders;

namespace PlantOps.Domain.UnitTests.WorkOrders;

public sealed class WorkOrderAssignTests
{
    [Fact]
    public void Assign_moves_an_approved_work_order_to_Assigned_and_records_the_technician_and_time()
    {
        var workOrder = TestWorkOrders.Approved();

        var result = workOrder.Assign(TestWorkOrders.TechnicianId, TestWorkOrders.AssignedAt);

        result.IsSuccess.Should().BeTrue();
        workOrder.Status.Should().Be(WorkOrderStatus.Assigned);
        workOrder.AssignedToUserId.Should().Be(TestWorkOrders.TechnicianId);
        workOrder.AssignedAtUtc.Should().Be(TestWorkOrders.AssignedAt);
        workOrder.StartedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Reassigning_while_Assigned_replaces_the_technician_and_assignment_time_but_keeps_the_approval()
    {
        var workOrder = TestWorkOrders.Assigned();

        var result = workOrder.Assign(TestWorkOrders.OtherTechnicianId, TestWorkOrders.ReassignedAt);

        result.IsSuccess.Should().BeTrue();
        workOrder.Status.Should().Be(WorkOrderStatus.Assigned);
        workOrder.AssignedToUserId.Should().Be(TestWorkOrders.OtherTechnicianId);
        workOrder.AssignedAtUtc.Should().Be(TestWorkOrders.ReassignedAt);
        workOrder.ApprovedByUserId.Should().Be(TestWorkOrders.SupervisorId);
        workOrder.ApprovedAtUtc.Should().Be(TestWorkOrders.ApprovedAt);
    }

    [Fact]
    public void Handover_from_InProgress_returns_to_Assigned_for_the_new_technician_and_clears_the_start_time()
    {
        var workOrder = TestWorkOrders.InProgress();

        var result = workOrder.Assign(TestWorkOrders.OtherTechnicianId, TestWorkOrders.ReassignedAt);

        result.IsSuccess.Should().BeTrue();
        workOrder.Status.Should().Be(WorkOrderStatus.Assigned);
        workOrder.AssignedToUserId.Should().Be(TestWorkOrders.OtherTechnicianId);
        workOrder.AssignedAtUtc.Should().Be(TestWorkOrders.ReassignedAt);
        workOrder.StartedAtUtc.Should().BeNull();
    }

    [Fact]
    public void After_handover_the_new_technician_cannot_close_without_starting_again()
    {
        var workOrder = TestWorkOrders.ReassignedToOtherTechnician(WorkOrderStatus.InProgress);

        var result = workOrder.Close(TestWorkOrders.OtherTechnicianId, TestWorkOrders.ResolutionNotes, TestWorkOrders.ClosedAt);

        result.Error.Should().Be(WorkOrderErrors.InvalidTransition(WorkOrderStatus.Assigned, WorkOrderStatus.Closed));
        workOrder.Status.Should().Be(WorkOrderStatus.Assigned);
    }

    [Fact]
    public void After_handover_the_new_technician_starts_the_work_with_a_new_start_time()
    {
        var workOrder = TestWorkOrders.ReassignedToOtherTechnician(WorkOrderStatus.InProgress);
        var restartedAt = TestWorkOrders.ReassignedAt.AddMinutes(15);

        var result = workOrder.Start(TestWorkOrders.OtherTechnicianId, restartedAt);

        result.IsSuccess.Should().BeTrue();
        workOrder.Status.Should().Be(WorkOrderStatus.InProgress);
        workOrder.StartedAtUtc.Should().Be(restartedAt);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Assigned)]
    [InlineData(WorkOrderStatus.InProgress)]
    public void After_reassignment_the_previous_technician_can_no_longer_start_the_work(WorkOrderStatus reassignedFrom)
    {
        var workOrder = TestWorkOrders.ReassignedToOtherTechnician(reassignedFrom);

        var result = workOrder.Start(TestWorkOrders.TechnicianId, TestWorkOrders.ReassignedAt.AddMinutes(15));

        result.Error.Should().Be(WorkOrderErrors.NotAssignedTechnician);
        workOrder.Status.Should().Be(WorkOrderStatus.Assigned);
    }
}
