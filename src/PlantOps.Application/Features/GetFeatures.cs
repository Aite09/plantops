using PlantOps.Application.Abstractions.Ai;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Domain.Common;

namespace PlantOps.Application.Features;

/// <summary>Optional features switched on in this installation, so the web app shows only what will work.</summary>
public sealed record FeaturesDto(bool ExplainFault);

public sealed record GetFeaturesQuery : IQuery<FeaturesDto>;

internal sealed class GetFeaturesQueryHandler(IFaultExplainer explainer) : IQueryHandler<GetFeaturesQuery, FeaturesDto>
{
    public Task<Result<FeaturesDto>> Handle(GetFeaturesQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(new FeaturesDto(explainer.IsEnabled)));
}
