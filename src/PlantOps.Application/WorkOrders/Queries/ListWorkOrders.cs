using FluentValidation;
using PlantOps.Application.Abstractions.Authentication;
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

internal sealed class ListWorkOrdersQueryHandler(IWorkOrderQueries queries, IUserDirectory userDirectory)
    : IQueryHandler<ListWorkOrdersQuery, PagedResult<WorkOrderSummaryDto>>
{
    public async Task<Result<PagedResult<WorkOrderSummaryDto>>> Handle(ListWorkOrdersQuery query, CancellationToken cancellationToken)
    {
        var page = await queries.ListAsync(query, cancellationToken);

        // One lookup for the whole page, not one per row.
        var assigneeIds = page.Items.Select(i => i.AssignedToUserId).OfType<Guid>().Distinct().ToArray();
        if (assigneeIds.Length == 0)
        {
            return page;
        }

        var names = await userDirectory.GetDisplayNamesAsync(assigneeIds, cancellationToken);
        var items = page.Items
            .Select(i => i.AssignedToUserId is { } id ? i with { AssignedToName = names.GetValueOrDefault(id) } : i)
            .ToList();

        return page with { Items = items };
    }
}
