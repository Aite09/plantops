using Moq;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.Common;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Queries;

public sealed class ListWorkOrdersQueryHandlerTests
{
    private static readonly Guid TechnicianA = Guid.NewGuid();
    private static readonly Guid TechnicianB = Guid.NewGuid();

    private readonly Mock<IWorkOrderQueries> _queries = new();
    private readonly Mock<IUserDirectory> _userDirectory = new();
    private readonly ListWorkOrdersQueryHandler _handler;

    public ListWorkOrdersQueryHandlerTests() =>
        _handler = new ListWorkOrdersQueryHandler(_queries.Object, _userDirectory.Object);

    [Fact]
    public async Task Assignee_names_are_filled_with_one_directory_lookup_for_the_whole_page()
    {
        var query = new ListWorkOrdersQuery(Page: 2, PageSize: 3);
        Returns(query, new PagedResult<WorkOrderSummaryDto>([Row(TechnicianA), Row(null), Row(TechnicianB), Row(TechnicianA)], 2, 3, 10));
        _userDirectory
            .Setup(d => d.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [TechnicianA] = "Alex Technician", [TechnicianB] = "Blair Technician" });

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.Value.Items.Select(i => i.AssignedToName).Should().Equal("Alex Technician", null, "Blair Technician", "Alex Technician");
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(3);
        result.Value.TotalCount.Should().Be(10);
        // Each assignee is looked up once, however many rows they appear on.
        _userDirectory.Verify(
            d => d.GetDisplayNamesAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(TechnicianA) && ids.Contains(TechnicianB)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Page_with_no_assigned_rows_is_returned_as_is_without_calling_the_directory()
    {
        var query = new ListWorkOrdersQuery();
        var page = new PagedResult<WorkOrderSummaryDto>([Row(null), Row(null)], 1, ListWorkOrdersQuery.DefaultPageSize, 2);
        Returns(query, page);

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.Value.Should().BeSameAs(page);
        _userDirectory.VerifyNoOtherCalls();
    }

    private void Returns(ListWorkOrdersQuery query, PagedResult<WorkOrderSummaryDto> page) =>
        _queries.Setup(q => q.ListAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(page);

    private static WorkOrderSummaryDto Row(Guid? assignedTo) => new(
        Guid.NewGuid(),
        "Mechanical seal leaking",
        WorkOrderPriority.High,
        assignedTo is null ? WorkOrderStatus.Open : WorkOrderStatus.Assigned,
        "U-100",
        "P-101A",
        assignedTo,
        AssignedToName: null,
        TestWorkOrders.CreatedAtUtc);
}
