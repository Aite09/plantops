using FluentValidation;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.WorkOrders.Queries;

public sealed record GetWorkOrderByIdQuery(Guid WorkOrderId) : IQuery<WorkOrderDetailsDto>;

internal sealed class GetWorkOrderByIdQueryValidator : AbstractValidator<GetWorkOrderByIdQuery>
{
    public GetWorkOrderByIdQueryValidator() => RuleFor(q => q.WorkOrderId).NotEmpty();
}

internal sealed class GetWorkOrderByIdQueryHandler(IWorkOrderQueries queries, IUserDirectory userDirectory)
    : IQueryHandler<GetWorkOrderByIdQuery, WorkOrderDetailsDto>
{
    public async Task<Result<WorkOrderDetailsDto>> Handle(GetWorkOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var details = await queries.GetDetailsAsync(query.WorkOrderId, cancellationToken);
        if (details is null)
        {
            return WorkOrderErrors.NotFound(query.WorkOrderId);
        }

        // Users live with the identity provider, not in the work-order tables, so names are looked up separately.
        Guid?[] userIds = [details.ReportedByUserId, details.ApprovedByUserId, details.AssignedToUserId];
        var names = await userDirectory.GetDisplayNamesAsync(
            userIds.OfType<Guid>().Distinct().ToArray(), cancellationToken);

        return details with
        {
            ReportedByName = names.GetValueOrDefault(details.ReportedByUserId),
            ApprovedByName = details.ApprovedByUserId is { } approvedBy ? names.GetValueOrDefault(approvedBy) : null,
            AssignedToName = details.AssignedToUserId is { } assignedTo ? names.GetValueOrDefault(assignedTo) : null,
        };
    }
}
