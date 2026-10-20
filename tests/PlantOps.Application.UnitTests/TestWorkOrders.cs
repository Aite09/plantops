using PlantOps.Domain.Plant;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests;

/// <summary>
/// Real <see cref="WorkOrder"/> aggregates in a given lifecycle state, built through the domain's own methods
/// so every invariant holds. Handlers are tested against these instead of mocks of the aggregate.
/// </summary>
internal static class TestWorkOrders
{
    /// <summary>When every work order built here was created and moved along. Earlier than any test clock.</summary>
    public static readonly DateTimeOffset CreatedAtUtc = new(2026, 3, 14, 6, 0, 0, TimeSpan.Zero);

    public static readonly Guid SupervisorId = Guid.Parse("5b0c7c1e-3a4f-4a8e-9f61-2d7e1c0b9a01");

    public static readonly Equipment Pump = new(
        Guid.Parse("0f3a8f52-6c1d-4f0e-8b7a-91c2d4e5f601"),
        Guid.Parse("7d2e9b14-0a3c-4b5d-8e6f-12a3b4c5d602"),
        "P-101A",
        "Crude charge pump A");

    public static WorkOrder Open(Guid reportedByUserId) =>
        WorkOrder.Create(
            "Mechanical seal leaking",
            "Steady drip from the inboard seal of P-101A.",
            WorkOrderPriority.High,
            Pump,
            reportedByUserId,
            CreatedAtUtc).Value;

    public static WorkOrder Approved(Guid reportedByUserId)
    {
        var workOrder = Open(reportedByUserId);
        workOrder.Approve(SupervisorId, CreatedAtUtc).IsSuccess.Should().BeTrue();
        return workOrder;
    }

    public static WorkOrder Assigned(Guid technicianId)
    {
        var workOrder = Approved(Guid.NewGuid());
        workOrder.Assign(technicianId, CreatedAtUtc).IsSuccess.Should().BeTrue();
        return workOrder;
    }

    public static WorkOrder InProgress(Guid technicianId)
    {
        var workOrder = Assigned(technicianId);
        workOrder.Start(technicianId, CreatedAtUtc).IsSuccess.Should().BeTrue();
        return workOrder;
    }

    public static WorkOrder Closed(Guid technicianId)
    {
        var workOrder = InProgress(technicianId);
        workOrder.Close(technicianId, "Replaced the seal.", CreatedAtUtc).IsSuccess.Should().BeTrue();
        return workOrder;
    }
}
