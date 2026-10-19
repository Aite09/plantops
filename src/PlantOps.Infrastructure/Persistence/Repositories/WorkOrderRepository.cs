using Microsoft.EntityFrameworkCore;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Infrastructure.Persistence.Repositories;

internal sealed class WorkOrderRepository(PlantOpsDbContext db) : IWorkOrderRepository
{
    // Tracked on purpose: the command changes the aggregate and SaveChanges writes only the changed columns.
    public Task<WorkOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.WorkOrders.SingleOrDefaultAsync(w => w.Id == id, cancellationToken);

    public void Add(WorkOrder workOrder) => db.WorkOrders.Add(workOrder);

    // EF Core puts the property's original value in the UPDATE's WHERE clause. Replacing it with the version the
    // client saw turns "changed since you loaded it" into DbUpdateConcurrencyException, which the API maps to 409.
    public void SetExpectedVersion(WorkOrder workOrder, byte[] version) =>
        db.Entry(workOrder).Property<byte[]>("RowVersion").OriginalValue = version;
}
