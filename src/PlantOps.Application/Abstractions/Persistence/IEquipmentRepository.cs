using PlantOps.Domain.Plant;

namespace PlantOps.Application.Abstractions.Persistence;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
