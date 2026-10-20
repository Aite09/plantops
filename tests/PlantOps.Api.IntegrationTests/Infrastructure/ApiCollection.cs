namespace PlantOps.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Every integration test class joins this collection, so they share one API + SQL Server and run one at a time
/// (they reset the same database, so running them in parallel would make them interfere).
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PlantOpsApiFactory>
{
    public const string Name = "Api";
}
