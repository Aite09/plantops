using FluentValidation;
using PlantOps.Application.Abstractions.Ai;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.WorkOrders.Queries;

/// <summary>Plain-text suggestions from <see cref="Model"/>. Advisory only: never saved to the work order.</summary>
public sealed record FaultExplanationDto(string Text, string Model, DateTimeOffset GeneratedAtUtc);

/// <summary>
/// "Explain this fault". A query, not a command: it changes nothing in PlantOps, even though the API exposes it as a
/// POST (it is slow and uses up a rate limit, so nothing should prefetch or cache it on the way).
/// </summary>
public sealed record ExplainWorkOrderFaultQuery(Guid WorkOrderId) : IQuery<FaultExplanationDto>;

internal sealed class ExplainWorkOrderFaultQueryValidator : AbstractValidator<ExplainWorkOrderFaultQuery>
{
    public ExplainWorkOrderFaultQueryValidator() => RuleFor(q => q.WorkOrderId).NotEmpty();
}

internal sealed class ExplainWorkOrderFaultQueryHandler(IWorkOrderQueries queries, IFaultExplainer explainer)
    : IQueryHandler<ExplainWorkOrderFaultQuery, FaultExplanationDto>
{
    public async Task<Result<FaultExplanationDto>> Handle(ExplainWorkOrderFaultQuery query, CancellationToken cancellationToken)
    {
        // Checked first, so a switched-off feature never touches the database.
        if (!explainer.IsEnabled)
        {
            return FaultExplanationErrors.Disabled;
        }

        var details = await queries.GetDetailsAsync(query.WorkOrderId, cancellationToken);
        if (details is null)
        {
            return WorkOrderErrors.NotFound(query.WorkOrderId);
        }

        var report = new FaultReport(
            details.EquipmentTag,
            details.EquipmentName,
            details.PlantUnitCode,
            details.PlantUnitName,
            details.Priority,
            details.Title,
            details.Description);
        return await explainer.ExplainAsync(report, cancellationToken);
    }
}
