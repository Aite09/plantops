namespace PlantOps.Infrastructure;

public static class HealthCheckTags
{
    /// <summary>Checks that must pass before the app can serve requests (e.g. the database).</summary>
    public const string Ready = "ready";
}
