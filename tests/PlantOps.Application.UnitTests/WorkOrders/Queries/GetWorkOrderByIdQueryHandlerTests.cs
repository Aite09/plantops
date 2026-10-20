using Moq;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Queries;

public sealed class GetWorkOrderByIdQueryHandlerTests
{
    private static readonly Guid ReporterId = Guid.NewGuid();
    private static readonly Guid SupervisorId = Guid.NewGuid();
    private static readonly Guid TechnicianId = Guid.NewGuid();

    private readonly Mock<IWorkOrderQueries> _queries = new();
    private readonly Mock<IUserDirectory> _userDirectory = new();
    private readonly GetWorkOrderByIdQueryHandler _handler;

    public GetWorkOrderByIdQueryHandlerTests()
    {
        _userDirectory
            .Setup(d => d.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>
            {
                [ReporterId] = "Sam Staff",
                [SupervisorId] = "Pat Supervisor",
                [TechnicianId] = "Alex Technician",
            });
        _handler = new GetWorkOrderByIdQueryHandler(_queries.Object, _userDirectory.Object);
    }

    [Fact]
    public async Task Reporter_approver_and_assignee_names_are_filled_from_the_user_directory()
    {
        var details = Stored(Details(WorkOrderStatus.Assigned, approvedBy: SupervisorId, assignedTo: TechnicianId));

        var result = await _handler.Handle(new GetWorkOrderByIdQuery(details.Id), TestContext.Current.CancellationToken);

        result.Value.ReportedByName.Should().Be("Sam Staff");
        result.Value.ApprovedByName.Should().Be("Pat Supervisor");
        result.Value.AssignedToName.Should().Be("Alex Technician");
        _userDirectory.Verify(
            d => d.GetDisplayNamesAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 3 && ids.Contains(ReporterId) && ids.Contains(SupervisorId) && ids.Contains(TechnicianId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Work_order_not_yet_approved_or_assigned_looks_up_only_the_reporter_and_leaves_the_other_names_empty()
    {
        var details = Stored(Details(WorkOrderStatus.Open, approvedBy: null, assignedTo: null));

        var result = await _handler.Handle(new GetWorkOrderByIdQuery(details.Id), TestContext.Current.CancellationToken);

        result.Value.ReportedByName.Should().Be("Sam Staff");
        result.Value.ApprovedByName.Should().BeNull();
        result.Value.AssignedToName.Should().BeNull();
        _userDirectory.Verify(
            d => d.GetDisplayNamesAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { ReporterId })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Missing_work_order_returns_NotFound_without_looking_up_any_names()
    {
        var missingId = Guid.NewGuid();

        var result = await _handler.Handle(new GetWorkOrderByIdQuery(missingId), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.NotFound(missingId));
        _userDirectory.VerifyNoOtherCalls();
    }

    private WorkOrderDetailsDto Stored(WorkOrderDetailsDto details)
    {
        _queries.Setup(q => q.GetDetailsAsync(details.Id, It.IsAny<CancellationToken>())).ReturnsAsync(details);
        return details;
    }

    private static WorkOrderDetailsDto Details(WorkOrderStatus status, Guid? approvedBy, Guid? assignedTo) => new(
        Id: Guid.NewGuid(),
        Title: "Mechanical seal leaking",
        Description: "Steady drip from the inboard seal.",
        Priority: WorkOrderPriority.High,
        Status: status,
        PlantUnitId: Guid.NewGuid(),
        PlantUnitCode: "U-100",
        PlantUnitName: "Crude Distillation",
        EquipmentId: Guid.NewGuid(),
        EquipmentTag: "P-101A",
        EquipmentName: "Crude charge pump A",
        ReportedByUserId: ReporterId,
        ReportedByName: null,
        ApprovedByUserId: approvedBy,
        ApprovedByName: null,
        AssignedToUserId: assignedTo,
        AssignedToName: null,
        ResolutionNotes: null,
        CreatedAtUtc: TestWorkOrders.CreatedAtUtc,
        ApprovedAtUtc: approvedBy is null ? null : TestWorkOrders.CreatedAtUtc,
        AssignedAtUtc: assignedTo is null ? null : TestWorkOrders.CreatedAtUtc,
        StartedAtUtc: null,
        ClosedAtUtc: null,
        Version: [0, 0, 0, 0, 0, 0, 7, 209]);
}
