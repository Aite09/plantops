using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Domain.UnitTests.WorkOrders;

public sealed class WorkOrderStateMachineTests
{
    // The intended lifecycle, written out independently of the aggregate's own table so the two are checked
    // against each other. Assigned → Assigned is a reassignment; InProgress → Assigned is a shift handover.
    private static readonly HashSet<(WorkOrderStatus From, WorkOrderStatus To)> IntendedTransitions =
    [
        (WorkOrderStatus.Open, WorkOrderStatus.Approved),
        (WorkOrderStatus.Approved, WorkOrderStatus.Assigned),
        (WorkOrderStatus.Assigned, WorkOrderStatus.Assigned),
        (WorkOrderStatus.Assigned, WorkOrderStatus.InProgress),
        (WorkOrderStatus.InProgress, WorkOrderStatus.Assigned),
        (WorkOrderStatus.InProgress, WorkOrderStatus.Closed),
    ];

    public static readonly MatrixTheoryData<WorkOrderStatus, WorkOrderStatus> EveryStatusPair = new(
        Enum.GetValues<WorkOrderStatus>(),
        Enum.GetValues<WorkOrderStatus>());

    // Open is never a target: nothing moves a work order back to Open, so there is no command for it.
    public static readonly MatrixTheoryData<WorkOrderStatus, WorkOrderStatus> EveryStatusAndCommandTarget = new(
        Enum.GetValues<WorkOrderStatus>(),
        [WorkOrderStatus.Approved, WorkOrderStatus.Assigned, WorkOrderStatus.InProgress, WorkOrderStatus.Closed]);

    [Theory]
    [MemberData(nameof(EveryStatusPair))]
    public void CanTransitionTo_allows_exactly_the_intended_transitions(WorkOrderStatus from, WorkOrderStatus to)
    {
        var workOrder = TestWorkOrders.InStatus(from);

        workOrder.CanTransitionTo(to).Should().Be(IntendedTransitions.Contains((from, to)));
    }

    [Theory]
    [MemberData(nameof(EveryStatusAndCommandTarget))]
    public void Command_succeeds_exactly_when_the_transition_is_intended(WorkOrderStatus from, WorkOrderStatus to)
    {
        var workOrder = TestWorkOrders.InStatus(from);

        // Roles are enforced by the API, not the aggregate, so one user can drive every command. It is the technician
        // TestWorkOrders assigns, so the assignee check in Start and Close passes and only the transition table decides.
        var result = MoveTo(workOrder, to, TestWorkOrders.TechnicianId);

        if (IntendedTransitions.Contains((from, to)))
        {
            result.IsSuccess.Should().BeTrue();
            workOrder.Status.Should().Be(to);
        }
        else
        {
            result.Error.Should().Be(WorkOrderErrors.InvalidTransition(from, to));
            workOrder.Should().BeEquivalentTo(TestWorkOrders.InStatus(from), options => options.Excluding(w => w.Id));
        }
    }

    [Fact]
    public void An_invalid_transition_is_reported_as_a_conflict()
    {
        var workOrder = TestWorkOrders.Open();

        var result = workOrder.Start(TestWorkOrders.TechnicianId, TestWorkOrders.StartedAt);

        result.Error.Code.Should().Be("WorkOrder.InvalidTransition");
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Description.Should().Be("A work order cannot move from Open to InProgress.");
    }

    [Theory]
    [InlineData(WorkOrderStatus.Open, WorkOrderStatus.Approved)]
    [InlineData(WorkOrderStatus.Approved, WorkOrderStatus.Assigned)]
    [InlineData(WorkOrderStatus.Assigned, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.Closed)]
    public void Command_with_an_empty_acting_user_id_is_rejected_and_changes_nothing(WorkOrderStatus from, WorkOrderStatus to)
    {
        var workOrder = TestWorkOrders.InStatus(from);

        var result = MoveTo(workOrder, to, Guid.Empty);

        result.Error.Should().Be(WorkOrderErrors.UserRequired);
        workOrder.Should().BeEquivalentTo(TestWorkOrders.InStatus(from), options => options.Excluding(w => w.Id));
    }

    private static Result MoveTo(WorkOrder workOrder, WorkOrderStatus target, Guid actingUserId)
    {
        var now = TestWorkOrders.ReportedAt.AddDays(1);

        return target switch
        {
            WorkOrderStatus.Approved => workOrder.Approve(actingUserId, now),
            WorkOrderStatus.Assigned => workOrder.Assign(actingUserId, now),
            WorkOrderStatus.InProgress => workOrder.Start(actingUserId, now),
            WorkOrderStatus.Closed => workOrder.Close(actingUserId, TestWorkOrders.ResolutionNotes, now),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "No command moves a work order to this status."),
        };
    }
}
