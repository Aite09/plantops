using FluentValidation;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.Common;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.WorkOrders.Queries;

/// <summary>Filtered, paged list. Sorted by priority (highest first), then oldest first, so urgent stale work is on top.</summary>
public sealed record ListWorkOrdersQuery(
    WorkOrderStatus? Status = null,
    Guid? PlantUnitId = null,
    WorkOrderPriority? Priority = null,
    int Page = 1,
    int PageSize = ListWorkOrdersQuery.DefaultPageSize) : IQuery<PagedResult<WorkOrderSummaryDto>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // Keeps (Page - 1) * PageSize inside int range, so a huge page number is a 400, not an overflow.
    public const int MaxPage = int.MaxValue / MaxPageSize;
}

internal sealed class ListWorkOrdersQueryValidator : AbstractValidator<ListWorkOrdersQuery>
{
    public ListWorkOrdersQueryValidator()
    {
        RuleFor(q => q.Status).IsInEnum();
        RuleFor(q => q.Priority).IsInEnum();
        RuleFor(q => q.Page).InclusiveBetween(1, ListWorkOrdersQuery.MaxPage);
        RuleFor(q => q.PageSize).InclusiveBetween(1, ListWorkOrdersQuery.MaxPageSize);
    }
}

internal sealed class ListWorkOrdersQueryHandler(IWorkOrderQueries queries)
    : IQueryHandler<ListWorkOrdersQuery, PagedResult<WorkOrderSummaryDto>>
{
    public async Task<Result<PagedResult<WorkOrderSummaryDto>>> Handle(ListWorkOrdersQuery query, CancellationToken cancellationToken) =>
        await queries.ListAsync(query, cancellationToken);
}
