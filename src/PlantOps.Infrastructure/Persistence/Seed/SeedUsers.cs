namespace PlantOps.Infrastructure.Persistence.Seed;

/// <summary>
/// Fixed ids for the demo users. Phase 3 creates the matching ASP.NET Core Identity accounts with these
/// same ids, so the sample work orders below line up with real logins.
/// </summary>
public static class SeedUsers
{
    public static readonly Guid Staff = new("3c9b7d52-0e41-4f8a-b6d3-0000000000a1");
    public static readonly Guid Supervisor = new("3c9b7d52-0e41-4f8a-b6d3-0000000000b1");
    public static readonly Guid TechnicianA = new("3c9b7d52-0e41-4f8a-b6d3-0000000000c1");
    public static readonly Guid TechnicianB = new("3c9b7d52-0e41-4f8a-b6d3-0000000000c2");
}
