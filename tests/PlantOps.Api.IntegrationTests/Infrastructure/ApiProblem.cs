namespace PlantOps.Api.IntegrationTests.Infrastructure;

/// <summary>
/// An RFC 7807 problem body as the API sends it: the standard fields, the API's "code" extension and, for 400s,
/// the per-field errors keyed by the JSON field name.
/// </summary>
public sealed record ApiProblem(
    string? Title,
    int? Status,
    string? Detail,
    string? Code,
    IReadOnlyDictionary<string, string[]>? Errors);
