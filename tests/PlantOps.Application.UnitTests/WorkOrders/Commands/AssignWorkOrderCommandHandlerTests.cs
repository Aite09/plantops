using Microsoft.Extensions.Time.Testing;
using Moq;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.Users;
using PlantOps.Application.WorkOrders.Commands;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Commands;

public sealed class AssignWorkOrderCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 8, 15, 0, TimeSpan.Zero);
    private static readonly Guid TechnicianId = Guid.NewGuid();

    private readonly Mock<IWorkOrderRepository> _workOrders = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserDirectory> _userDirectory = new();
    private readonly AssignWorkOrderCommandHandler _handler;

    public AssignWorkOrderCommandHandlerTests()
    {
        // Only TechnicianId holds the Technician role; the loose mock answers false for anyone or anything else.
        _userDirectory
            .Setup(d => d.IsInRoleAsync(TechnicianId, Roles.Technician, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _handler = new AssignWorkOrderCommandHandler(
            _workOrders.Object, _unitOfWork.Object, _userDirectory.Object, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task Approved_work_order_is_assigned_to_the_technician_now_and_saved_once()
    {
        var workOrder = Stored(TestWorkOrders.Approved(Guid.NewGuid()));

        var result = await _handler.Handle(
            new AssignWorkOrderCommand(workOrder.Id, TechnicianId), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        workOrder.Status.Should().Be(WorkOrderStatus.Assigned);
        workOrder.AssignedToUserId.Should().Be(TechnicianId);
        workOrder.AssignedAtUtc.Should().Be(Now);
        _unitOfWork.Verify(u => u.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Assignee_who_is_not_a_technician_returns_User_NotATechnician_and_nothing_is_saved()
    {
        var workOrder = Stored(TestWorkOrders.Approved(Guid.NewGuid()));
        var staffUserId = Guid.NewGuid();

        var result = await _handler.Handle(
            new AssignWorkOrderCommand(workOrder.Id, staffUserId), TestContext.Current.CancellationToken);

        result.Error.Should().Be(UserErrors.NotATechnician(staffUserId));
        workOrder.Status.Should().Be(WorkOrderStatus.Approved);
        workOrder.AssignedToUserId.Should().BeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_work_order_returns_NotFound_and_nothing_is_saved()
    {
        var missingId = Guid.NewGuid();

        var result = await _handler.Handle(
            new AssignWorkOrderCommand(missingId, TechnicianId), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.NotFound(missingId));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Work_order_that_is_not_yet_approved_returns_the_domain_conflict_and_nothing_is_saved()
    {
        var workOrder = Stored(TestWorkOrders.Open(Guid.NewGuid()));

        var result = await _handler.Handle(
            new AssignWorkOrderCommand(workOrder.Id, TechnicianId), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.InvalidTransition(WorkOrderStatus.Open, WorkOrderStatus.Assigned));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private WorkOrder Stored(WorkOrder workOrder)
    {
        _workOrders.Setup(r => r.GetByIdAsync(workOrder.Id, It.IsAny<CancellationToken>())).ReturnsAsync(workOrder);
        return workOrder;
    }
}
