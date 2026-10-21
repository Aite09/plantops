using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.Abstractions.Ai;

/// <summary>
/// Port to a language model that suggests safety points, likely causes and first checks for a reported fault.
/// The Application layer only knows this interface; which model and provider answer it is Infrastructure
/// configuration.
/// </summary>
public interface IFaultExplainer
{
    /// <summary>False unless the feature is switched on in configuration. It is off by default.</summary>
    bool IsEnabled { get; }

    Task<Result<FaultExplanationDto>> ExplainAsync(FaultReport report, CancellationToken cancellationToken);
}

/// <summary>What the model is told about a fault: the equipment and the reporter's words, never who reported it.</summary>
public sealed record FaultReport(
    string EquipmentTag,
    string EquipmentName,
    string PlantUnitCode,
    string PlantUnitName,
    WorkOrderPriority Priority,
    string Title,
    string Description);

public static class FaultExplanationErrors
{
    // 404, like an endpoint that doesn't exist: in this installation it doesn't.
    public static readonly Error Disabled = Error.NotFound(
        "FaultExplanation.Disabled", "Explaining faults is switched off for this PlantOps installation.");

    public static readonly Error Unavailable = Error.Unavailable(
        "FaultExplanation.Unavailable", "The AI model did not answer. The work order is unaffected; try again in a minute.");
}
