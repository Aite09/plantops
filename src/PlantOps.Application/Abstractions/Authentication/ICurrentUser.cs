namespace PlantOps.Application.Abstractions.Authentication;

/// <summary>
/// The user making the current request. Handlers depend on this, not on HttpContext or a specific
/// identity provider, so the authentication mechanism can change without touching use cases.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Id of the authenticated user. Throws if called on an anonymous request.</summary>
    Guid UserId { get; }
}
