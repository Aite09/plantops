using Moq;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Queries;

public sealed class GetWorkOrderHistoryQueryHandlerTests
{
    private static readonly Guid WorkOrderId = Guid.NewGuid();
    private static readonly Guid ReporterId = Guid.NewGuid();
    private static readonly Guid SupervisorId = Guid.NewGuid();

    private readonly Mock<IWorkOrderQueries> _workOrderQueries = new();
    private readonly Mock<IAuditLogQueries> _auditLogQueries = new();
    private readonly Mock<IUserDirectory> _userDirectory = new();
    private readonly GetWorkOrderHistoryQueryHandler _handler;

    public GetWorkOrderHistoryQueryHandlerTests()
    {
        _workOrderQueries.Setup(q => q.ExistsAsync(WorkOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _handler = new GetWorkOrderHistoryQueryHandler(_workOrderQueries.Object, _auditLogQueries.Object, _userDirectory.Object);
    }

    [Fact]
    public async Task User_names_are_filled_on_each_entry_in_the_original_order()
    {
        // A null user is a change saved without a signed-in user (e.g. seed data at startup): no name to look up.
        HistoryIs(Entry("Created", ReporterId), Entry("Updated", ReporterId), Entry("Updated", null), Entry("Approved", SupervisorId));
        _userDirectory
            .Setup(d => d.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [ReporterId] = "Sam Staff", [SupervisorId] = "Pat Supervisor" });

        var result = await _handler.Handle(new GetWorkOrderHistoryQuery(WorkOrderId), TestContext.Current.CancellationToken);

        result.Value.Select(e => e.Action).Should().Equal("Created", "Updated", "Updated", "Approved");
        result.Value.Select(e => e.UserName).Should().Equal("Sam Staff", "Sam Staff", null, "Pat Supervisor");
        _userDirectory.Verify(
            d => d.GetDisplayNamesAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(ReporterId) && ids.Contains(SupervisorId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task User_the_directory_no_longer_knows_gets_no_name_instead_of_failing_the_request()
    {
        // Audit entries outlive accounts, so an old entry can carry an id the directory cannot resolve any more.
        var removedUserId = Guid.NewGuid();
        HistoryIs(Entry("Created", removedUserId), Entry("Approved", SupervisorId));
        _userDirectory
            .Setup(d => d.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [SupervisorId] = "Pat Supervisor" });

        var result = await _handler.Handle(new GetWorkOrderHistoryQuery(WorkOrderId), TestContext.Current.CancellationToken);

        result.Value.Select(e => e.UserName).Should().Equal(null, "Pat Supervisor");
        result.Value[0].UserId.Should().Be(removedUserId);
    }

    [Fact]
    public async Task History_is_read_for_the_WorkOrder_entity_type()
    {
        HistoryIs();

        await _handler.Handle(new GetWorkOrderHistoryQuery(WorkOrderId), TestContext.Current.CancellationToken);

        _auditLogQueries.Verify(q => q.GetHistoryAsync("WorkOrder", WorkOrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task History_with_no_user_changes_does_not_call_the_directory()
    {
        HistoryIs(Entry("Updated", null));

        var result = await _handler.Handle(new GetWorkOrderHistoryQuery(WorkOrderId), TestContext.Current.CancellationToken);

        result.Value.Should().ContainSingle().Which.UserName.Should().BeNull();
        _userDirectory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Missing_work_order_returns_NotFound_without_reading_the_audit_log()
    {
        var missingId = Guid.NewGuid();

        var result = await _handler.Handle(new GetWorkOrderHistoryQuery(missingId), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.NotFound(missingId));
        _auditLogQueries.VerifyNoOtherCalls();
        _userDirectory.VerifyNoOtherCalls();
    }

    private void HistoryIs(params AuditEntryDto[] entries) =>
        _auditLogQueries
            .Setup(q => q.GetHistoryAsync(It.IsAny<string>(), WorkOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

    private static AuditEntryDto Entry(string action, Guid? userId) =>
        new(TestWorkOrders.CreatedAtUtc, action, userId, UserName: null, Changes: []);
}
