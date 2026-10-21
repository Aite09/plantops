using Microsoft.Extensions.Time.Testing;
using Moq;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.Users;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Queries;

public sealed class GetDashboardQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 16, 6, 0, 0, TimeSpan.Zero);
    private static readonly UserDto Alex = new(Guid.NewGuid(), "tech.a@plantops.test", "Alex Technician", [Roles.Technician]);
    private static readonly UserDto Blair = new(Guid.NewGuid(), "tech.b@plantops.test", "Blair Technician", [Roles.Technician]);

    private readonly Mock<IWorkOrderQueries> _queries = new();
    private readonly Mock<IUserDirectory> _userDirectory = new();
    private readonly GetDashboardQueryHandler _handler;

    public GetDashboardQueryHandlerTests()
    {
        _userDirectory
            .Setup(d => d.ListAsync(Roles.Technician, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Alex, Blair]);
        _handler = new GetDashboardQueryHandler(_queries.Object, _userDirectory.Object, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task Work_reported_more_than_seven_days_ago_counts_as_ageing_and_the_snapshot_is_stamped_with_the_current_time()
    {
        Returns(new DashboardData([], 0, [], []));

        var result = await _handler.Handle(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        _queries.Verify(q => q.GetDashboardAsync(Now.AddDays(-7), 25, It.IsAny<CancellationToken>()), Times.Once);
        result.Value.GeneratedAtUtc.Should().Be(Now);
        result.Value.AgeingAfterDays.Should().Be(7);
    }

    [Fact]
    public async Task Every_technician_is_listed_in_directory_order_and_one_with_no_work_shows_zero()
    {
        Returns(new DashboardData([], 0, [], [new TechnicianWorkCount(Blair.Id, Assigned: 2, InProgress: 1)]));

        var result = await _handler.Handle(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.Value.Technicians.Should().Equal(
            new TechnicianWorkloadDto(Alex.Id, "Alex Technician", 0, 0),
            new TechnicianWorkloadDto(Blair.Id, "Blair Technician", 2, 1));
    }

    [Fact]
    public async Task Assignees_in_the_needs_attention_list_are_named_from_the_technician_list_without_another_lookup()
    {
        Returns(new DashboardData([Row(Blair.Id), Row(null)], 2, [], [new TechnicianWorkCount(Blair.Id, 1, 0)]));

        var result = await _handler.Handle(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.Value.NeedsAttention.Select(i => i.AssignedToName).Should().Equal("Blair Technician", null);
        result.Value.NeedsAttentionTotal.Should().Be(2);
        _userDirectory.Verify(d => d.ListAsync(Roles.Technician, It.IsAny<CancellationToken>()), Times.Once);
        _userDirectory.VerifyNoOtherCalls();
    }

    private void Returns(DashboardData data) =>
        _queries
            .Setup(q => q.GetDashboardAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

    private static WorkOrderSummaryDto Row(Guid? assignedTo) => new(
        Guid.NewGuid(),
        "Pump not reaching rated flow",
        WorkOrderPriority.High,
        assignedTo is null ? WorkOrderStatus.Open : WorkOrderStatus.Assigned,
        "U-200",
        "P-205",
        assignedTo,
        null,
        Now.AddDays(-9));
}
