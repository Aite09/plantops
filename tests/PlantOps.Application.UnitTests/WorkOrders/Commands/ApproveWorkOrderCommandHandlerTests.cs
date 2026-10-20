using Microsoft.Extensions.Time.Testing;
using Moq;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.WorkOrders.Commands;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Commands;

public sealed class ApproveWorkOrderCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 8, 15, 0, TimeSpan.Zero);
    private static readonly Guid SupervisorId = Guid.NewGuid();

    private readonly Mock<IWorkOrderRepository> _workOrders = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly ApproveWorkOrderCommandHandler _handler;

    public ApproveWorkOrderCommandHandlerTests()
    {
        _currentUser.Setup(u => u.UserId).Returns(SupervisorId);
        _handler = new ApproveWorkOrderCommandHandler(
            _workOrders.Object, _unitOfWork.Object, _currentUser.Object, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task Open_work_order_is_approved_by_the_current_user_now_and_saved_once()
    {
        var workOrder = Stored(TestWorkOrders.Open(Guid.NewGuid()));

        var result = await _handler.Handle(new ApproveWorkOrderCommand(workOrder.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        workOrder.Status.Should().Be(WorkOrderStatus.Approved);
        workOrder.ApprovedByUserId.Should().Be(SupervisorId);
        workOrder.ApprovedAtUtc.Should().Be(Now);
        _unitOfWork.Verify(u => u.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Missing_work_order_returns_NotFound_and_nothing_is_saved()
    {
        var missingId = Guid.NewGuid();

        var result = await _handler.Handle(new ApproveWorkOrderCommand(missingId), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.NotFound(missingId));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Already_approved_work_order_returns_the_domain_conflict_and_nothing_is_saved()
    {
        var workOrder = Stored(TestWorkOrders.Approved(Guid.NewGuid()));

        var result = await _handler.Handle(new ApproveWorkOrderCommand(workOrder.Id), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.InvalidTransition(WorkOrderStatus.Approved, WorkOrderStatus.Approved));
        workOrder.ApprovedByUserId.Should().Be(TestWorkOrders.SupervisorId);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private WorkOrder Stored(WorkOrder workOrder)
    {
        _workOrders.Setup(r => r.GetByIdAsync(workOrder.Id, It.IsAny<CancellationToken>())).ReturnsAsync(workOrder);
        return workOrder;
    }
}
