using PlantOps.Application.Auth;
using PlantOps.Domain.Common;

namespace PlantOps.Application.Abstractions.Authentication;

/// <summary>
/// Operations that only exist because PlantOps hosts its own accounts (ASP.NET Core Identity).
/// With Microsoft Entra ID, sign-in and account creation happen in Entra, and this interface goes away.
/// </summary>
public interface IAccountService
{
    Task<Result<AccessTokenDto>> LoginAsync(string email, string password, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateUserAsync(string email, string displayName, string password, string role, CancellationToken cancellationToken);
}
