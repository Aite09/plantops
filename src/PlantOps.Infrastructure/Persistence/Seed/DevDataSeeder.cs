using Microsoft.EntityFrameworkCore;
using PlantOps.Domain.Plant;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Infrastructure.Persistence.Seed;

/// <summary>
/// Sample work orders for local development only. Built through the aggregate's own methods,
/// so even seed data has to obey the status rules.
/// </summary>
internal static class DevDataSeeder
{
    private sealed record Sample(
        string EquipmentTag,
        string Title,
        string Description,
        WorkOrderPriority Priority,
        int AgeInDays,
        WorkOrderStatus TargetStatus,
        Guid? Technician = null);

    private static readonly Sample[] Samples =
    [
        new("P-101A", "Mechanical seal leaking", "Steady drip of crude from the inboard seal on P-101A. Puddle forming under the baseplate.",
            WorkOrderPriority.High, 1, WorkOrderStatus.Open),
        new("E-102", "Rising outlet temperature", "Preheat exchanger outlet running 15 °C above normal. Possible fouling on the tube side.",
            WorkOrderPriority.Medium, 3, WorkOrderStatus.Open),
        new("CT-301", "Fan vibration alarm", "High vibration alarm on cooling tower fan 1 gearbox, reading 9 mm/s and climbing.",
            WorkOrderPriority.Critical, 0, WorkOrderStatus.Approved),
        new("B-401", "Burner flame scanner fault", "Flame scanner dropping out intermittently, causing nuisance boiler trips.",
            WorkOrderPriority.High, 5, WorkOrderStatus.Approved),
        new("C-201", "Lube oil filter high dP", "Differential pressure across the compressor lube oil filter is at the alarm limit.",
            WorkOrderPriority.Medium, 6, WorkOrderStatus.Assigned, SeedUsers.TechnicianA),
        new("P-205", "Pump not reaching rated flow", "Feed pump delivering about 80% of rated flow at normal suction pressure.",
            WorkOrderPriority.High, 9, WorkOrderStatus.Assigned, SeedUsers.TechnicianB),
        new("F-101", "Damaged heater sight glass", "Cracked sight glass on the west peephole of the charge heater.",
            WorkOrderPriority.Low, 12, WorkOrderStatus.InProgress, SeedUsers.TechnicianA),
        new("P-301B", "Coupling guard missing", "Coupling guard removed during last job and not refitted. Pump isolated pending repair.",
            WorkOrderPriority.Medium, 15, WorkOrderStatus.InProgress, SeedUsers.TechnicianB),
        new("PSV-410", "Safety valve simmering", "Steam drum PSV simmering below set pressure. Needs bench test and reset.",
            WorkOrderPriority.Critical, 20, WorkOrderStatus.Closed, SeedUsers.TechnicianA),
        new("R-201", "Thermocouple reading erratic", "Bed thermocouple TI-2014 jumping between 340 and 390 °C.",
            WorkOrderPriority.Medium, 28, WorkOrderStatus.Closed, SeedUsers.TechnicianB),
    ];

    public static async Task SeedAsync(PlantOpsDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await db.WorkOrders.AnyAsync(cancellationToken))
        {
            return;
        }

        var equipmentByTag = await db.Equipment.ToDictionaryAsync(e => e.Tag, cancellationToken);

        foreach (var sample in Samples)
        {
            db.WorkOrders.Add(Build(sample, equipmentByTag[sample.EquipmentTag], now));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static WorkOrder Build(Sample sample, Equipment equipment, DateTimeOffset now)
    {
        var reportedAt = now.AddDays(-sample.AgeInDays).AddHours(-2);
        var workOrder = WorkOrder.Create(sample.Title, sample.Description, sample.Priority, equipment, SeedUsers.Staff, reportedAt).Value;

        if (sample.TargetStatus == WorkOrderStatus.Open)
        {
            return workOrder;
        }

        Ensure(workOrder.Approve(SeedUsers.Supervisor, reportedAt.AddHours(1)));
        if (sample.TargetStatus == WorkOrderStatus.Approved)
        {
            return workOrder;
        }

        var technician = sample.Technician ?? throw new InvalidOperationException($"Sample '{sample.Title}' needs a technician.");
        Ensure(workOrder.Assign(technician, reportedAt.AddHours(2)));
        if (sample.TargetStatus == WorkOrderStatus.Assigned)
        {
            return workOrder;
        }

        Ensure(workOrder.Start(technician, reportedAt.AddHours(4)));
        if (sample.TargetStatus == WorkOrderStatus.InProgress)
        {
            return workOrder;
        }

        Ensure(workOrder.Close(technician, "Repaired and returned to service. Tested OK with operations.", reportedAt.AddDays(1)));
        return workOrder;
    }

    private static void Ensure(Domain.Common.Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Seed data broke a domain rule: {result.Error.Description}");
        }
    }
}
