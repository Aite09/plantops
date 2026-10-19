namespace PlantOps.Application.Abstractions.Persistence;

/// <summary>Commits every change made through the repositories in one database transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
