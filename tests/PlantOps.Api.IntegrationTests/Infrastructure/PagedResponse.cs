namespace PlantOps.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A page of the work-order list as it travels on the wire. Unlike the API's own PagedResult, TotalPages is read
/// from the JSON here rather than recomputed, so the tests check what the web app actually receives.
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
