using PlantOps.Domain.WorkOrders;

namespace PlantOps.Api.Contracts;

// HTTP request/response shapes. Kept separate from the Application commands so the public API contract can stay
// stable while internals change, and so route values (the work order id) are not duplicated in the body.

public sealed record CreateWorkOrderRequest(string Title, string Description, WorkOrderPriority Priority, Guid EquipmentId);

/// <summary>Send back the <c>version</c> from GET /api/work-orders/{id} unchanged (base64).</summary>
public sealed record UpdateWorkOrderRequest(string Title, string Description, WorkOrderPriority Priority, byte[] Version);

public sealed record AssignWorkOrderRequest(Guid TechnicianId);

public sealed record CloseWorkOrderRequest(string ResolutionNotes);

public sealed record CreatedResponse(Guid Id);
