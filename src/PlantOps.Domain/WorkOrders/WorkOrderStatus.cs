namespace PlantOps.Domain.WorkOrders;

/// <summary>Lifecycle of a work order. Allowed moves are defined in <see cref="WorkOrder"/>.</summary>
public enum WorkOrderStatus
{
    Open,
    Approved,
    Assigned,
    InProgress,
    Closed,
}
