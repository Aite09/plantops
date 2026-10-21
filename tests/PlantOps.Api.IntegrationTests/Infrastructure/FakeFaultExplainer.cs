using PlantOps.Application.Abstractions.Ai;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.Common;

namespace PlantOps.Api.IntegrationTests.Infrastructure;

/// <summary>Stands in for the AI adapter in API tests: switched on, and answering whatever the test sets.</summary>
public sealed class FakeFaultExplainer : IFaultExplainer
{
    public bool IsEnabled { get; init; } = true;

    public Result<FaultExplanationDto> Answer { get; set; } = new FaultExplanationDto(
        "Possible causes:\n- Worn seal faces", "fake-model", new DateTimeOffset(2026, 3, 16, 6, 0, 0, TimeSpan.Zero));

    public FaultReport? LastReport { get; private set; }

    public Task<Result<FaultExplanationDto>> ExplainAsync(FaultReport report, CancellationToken cancellationToken)
    {
        LastReport = report;
        return Task.FromResult(Answer);
    }
}
