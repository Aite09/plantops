namespace PlantOps.Application.Abstractions.Authentication;

/// <summary>
/// Role names, matching ASP.NET Core Identity roles today and Microsoft Entra ID app roles later,
/// so authorization policies keep working when the identity provider changes.
/// </summary>
public static class Roles
{
    public const string Staff = "Staff";
    public const string Supervisor = "Supervisor";
    public const string Technician = "Technician";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Staff, Supervisor, Technician, Admin];
}
