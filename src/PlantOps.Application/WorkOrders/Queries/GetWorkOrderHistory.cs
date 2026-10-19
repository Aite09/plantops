using FluentValidation;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.WorkOrders.Queries;

/// <summary>The audit trail of one work order: every change, who made it and when.</summary>
public sealed record GetWorkOrderHistoryQuery(Guid WorkOrderId) : IQuery<IReadOnlyList<AuditEntryDto>>;

internal sealed class GetWorkOrderHistoryQueryValidator : AbstractValidator<GetWorkOrderHistoryQuery>
{
    public GetWorkOrderHistoryQueryValidator() => RuleFor(q => q.WorkOrderId).NotEmpty();
}

internal sealed class GetWorkOrderHistoryQueryHandler(
    IWorkOrderQueries workOrderQueries,
    IAuditLogQueries auditLogQueries,
    IUserDirectory userDirectory) : IQueryHandler<GetWorkOrderHistoryQuery, IReadOnlyList<AuditEntryDto>>
{
    public async Task<Result<IReadOnlyList<AuditEntryDto>>> Handle(GetWorkOrderHistoryQuery query, CancellationToken cancellationToken)
    {
        if (!await workOrderQueries.ExistsAsync(query.WorkOrderId, cancellationToken))
        {
            return WorkOrderErrors.NotFound(query.WorkOrderId);
        }

        var history = await auditLogQueries.GetHistoryAsync(nameof(WorkOrder), query.WorkOrderId, cancellationToken);

        var userIds = history.Select(h => h.UserId).OfType<Guid>().Distinct().ToArray();
        var names = userIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await userDirectory.GetDisplayNamesAsync(userIds, cancellationToken);

        return history
            .Select(h => h.UserId is { } id ? h with { UserName = names.GetValueOrDefault(id) } : h)
            .ToList();
    }
}
