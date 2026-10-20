using Microsoft.Extensions.Time.Testing;
using Moq;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.WorkOrders.Commands;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Commands;

public sealed class StartWorkOrderCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 8, 15, 0, TimeSpan.Zero);
    private static readonly Guid TechnicianId = Guid.NewGuid();

    private readonly Mock<IWorkOrderRepository> _workOrders = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly StartWorkOrderCommandHandler _handler;

    public StartWorkOrderCommandHandlerTests()
    {
        _currentUser.Setup(u => u.UserId).Returns(TechnicianId);
        _handler = new StartWorkOrderCommandHandler(
            _workOrders.Object, _unitOfWork.Object, _currentUser.Object, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task Assigned_technician_starts_the_work_order_now_and_it_is_saved_once()
    {
        var workOrder = Stored(TestWorkOrders.Assigned(TechnicianId));

        var result = await _handler.Handle(new StartWorkOrderCommand(workOrder.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        workOrder.Status.Should().Be(WorkOrderStatus.InProgress);
        workOrder.StartedAtUtc.Should().Be(Now);
        _unitOfWork.Verify(u => u.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Missing_work_order_returns_NotFound_and_nothing_is_saved()
    {
        var missingId = Guid.NewGuid();

        var result = await _handler.Handle(new StartWorkOrderCommand(missingId), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.NotFound(missingId));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Start_by_a_technician_who_is_not_assigned_is_forbidden_and_nothing_is_saved()
    {
        var workOrder = Stored(TestWorkOrders.Assigned(Guid.NewGuid()));

        var result = await _handler.Handle(new StartWorkOrderCommand(workOrder.Id), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.NotAssignedTechnician);
        workOrder.Status.Should().Be(WorkOrderStatus.Assigned);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private WorkOrder Stored(WorkOrder workOrder)
    {
        _workOrders.Setup(r => r.GetByIdAsync(workOrder.Id, It.IsAny<CancellationToken>())).ReturnsAsync(workOrder);
        return workOrder;
    }
}
