namespace PlantOps.Domain.WorkOrders;

/// <summary>Explicit values because priority is stored as a number and sorted on; reordering members must not change data.</summary>
public enum WorkOrderPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}
