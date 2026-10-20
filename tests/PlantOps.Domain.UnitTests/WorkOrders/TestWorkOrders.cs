using PlantOps.Domain.Common;
using PlantOps.Domain.Plant;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Domain.UnitTests.WorkOrders;

/// <summary>
/// Known users, fixed times and work orders already moved to a given status through the aggregate's own methods,
/// so every test starts from a state the real lifecycle can produce.
/// </summary>
internal static class TestWorkOrders
{
    public const string ResolutionNotes = "Replaced the mechanical seal and flushed the seal pot.";

    public static readonly Guid ReporterId = new("6f1b7c1e-0000-4000-8000-000000000001");
    public static readonly Guid SupervisorId = new("6f1b7c1e-0000-4000-8000-000000000002");
    public static readonly Guid TechnicianId = new("6f1b7c1e-0000-4000-8000-000000000003");
    public static readonly Guid OtherTechnicianId = new("6f1b7c1e-0000-4000-8000-000000000004");

    public static readonly DateTimeOffset ReportedAt = new(2026, 3, 2, 6, 15, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset ApprovedAt = ReportedAt.AddMinutes(20);
    public static readonly DateTimeOffset AssignedAt = ReportedAt.AddMinutes(30);
    public static readonly DateTimeOffset StartedAt = ReportedAt.AddHours(1);
    public static readonly DateTimeOffset ClosedAt = ReportedAt.AddHours(3);
    public static readonly DateTimeOffset ReassignedAt = ReportedAt.AddHours(8);

    public static Equipment Pump() => new(
        new Guid("a7e3d2c1-0000-4000-8000-000000000101"),
        new Guid("a7e3d2c1-0000-4000-8000-000000000100"),
        "P-101A",
        "Crude charge pump A");

    public static WorkOrder Open()
    {
        var created = WorkOrder.Create(
            "Seal leak on P-101A",
            "Mechanical seal leaking at about one drop per second.",
            WorkOrderPriority.High,
            Pump(),
            ReporterId,
            ReportedAt);
        EnsureSucceeded(created);
        return created.Value;
    }

    public static WorkOrder Approved()
    {
        var workOrder = Open();
        EnsureSucceeded(workOrder.Approve(SupervisorId, ApprovedAt));
        return workOrder;
    }

    public static WorkOrder Assigned()
    {
        var workOrder = Approved();
        EnsureSucceeded(workOrder.Assign(TechnicianId, AssignedAt));
        return workOrder;
    }

    public static WorkOrder InProgress()
    {
        var workOrder = Assigned();
        EnsureSucceeded(workOrder.Start(TechnicianId, StartedAt));
        return workOrder;
    }

    public static WorkOrder Closed()
    {
        var workOrder = InProgress();
        EnsureSucceeded(workOrder.Close(TechnicianId, ResolutionNotes, ClosedAt));
        return workOrder;
    }

    public static WorkOrder InStatus(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Open => Open(),
        WorkOrderStatus.Approved => Approved(),
        WorkOrderStatus.Assigned => Assigned(),
        WorkOrderStatus.InProgress => InProgress(),
        WorkOrderStatus.Closed => Closed(),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown work order status."),
    };

    /// <summary>
    /// Takes the work order off <see cref="TechnicianId"/> and gives it to <see cref="OtherTechnicianId"/>:
    /// a reassignment from Assigned, or a shift handover from InProgress.
    /// </summary>
    public static WorkOrder ReassignedToOtherTechnician(WorkOrderStatus from)
    {
        var workOrder = InStatus(from);
        EnsureSucceeded(workOrder.Assign(OtherTechnicianId, ReassignedAt));
        return workOrder;
    }

    private static void EnsureSucceeded(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test setup failed: {result.Error.Code} {result.Error.Description}");
        }
    }
}
