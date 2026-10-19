using FluentValidation;
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

internal sealed class GetWorkOrderByIdQueryHandler(IWorkOrderQueries queries)
    : IQueryHandler<GetWorkOrderByIdQuery, WorkOrderDetailsDto>
{
    public async Task<Result<WorkOrderDetailsDto>> Handle(GetWorkOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var details = await queries.GetDetailsAsync(query.WorkOrderId, cancellationToken);
        return details is null
            ? WorkOrderErrors.NotFound(query.WorkOrderId)
            : details;
    }
}
