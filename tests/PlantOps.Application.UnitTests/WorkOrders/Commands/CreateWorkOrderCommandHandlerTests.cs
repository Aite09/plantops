using Microsoft.Extensions.Time.Testing;
using Moq;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.WorkOrders.Commands;
using PlantOps.Domain.Plant;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Commands;

public sealed class CreateWorkOrderCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 8, 15, 0, TimeSpan.Zero);
    private static readonly Guid ReporterId = Guid.NewGuid();
    private static readonly Equipment Pump = TestWorkOrders.Pump;

    private readonly Mock<IEquipmentRepository> _equipment = new();
    private readonly Mock<IWorkOrderRepository> _workOrders = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly CreateWorkOrderCommandHandler _handler;

    public CreateWorkOrderCommandHandlerTests()
    {
        _currentUser.Setup(u => u.UserId).Returns(ReporterId);
        _handler = new CreateWorkOrderCommandHandler(
            _equipment.Object, _workOrders.Object, _unitOfWork.Object, _currentUser.Object, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task Unknown_equipment_returns_Equipment_NotFound_and_nothing_is_added_or_saved()
    {
        var unknownId = Guid.NewGuid();
        var command = new CreateWorkOrderCommand("Seal leak", "Dripping.", WorkOrderPriority.High, unknownId);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EquipmentErrors.NotFound(unknownId));
        _workOrders.Verify(r => r.Add(It.IsAny<WorkOrder>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Valid_report_is_added_then_saved_once_and_its_id_is_returned()
    {
        // Saving before Add would commit nothing, so the order of the two calls is part of the behaviour.
        _equipment.Setup(r => r.GetByIdAsync(Pump.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Pump);
        var calls = new List<string>();
        WorkOrder? added = null;
        _workOrders.Setup(r => r.Add(It.IsAny<WorkOrder>())).Callback<WorkOrder>(w =>
        {
            added = w;
            calls.Add("Add");
        });
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("SaveChanges"));
        var command = new CreateWorkOrderCommand("Seal leak", "Dripping.", WorkOrderPriority.High, Pump.Id);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        calls.Should().Equal("Add", "SaveChanges");
        result.Value.Should().Be(added!.Id);
        _unitOfWork.Verify(u => u.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task New_work_order_is_reported_by_the_current_user_at_the_current_time_on_the_chosen_equipment()
    {
        _equipment.Setup(r => r.GetByIdAsync(Pump.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Pump);
        WorkOrder? added = null;
        _workOrders.Setup(r => r.Add(It.IsAny<WorkOrder>())).Callback<WorkOrder>(w => added = w);
        var command = new CreateWorkOrderCommand("Seal leak", "Dripping.", WorkOrderPriority.Critical, Pump.Id);

        await _handler.Handle(command, TestContext.Current.CancellationToken);

        added!.ReportedByUserId.Should().Be(ReporterId);
        added.CreatedAtUtc.Should().Be(Now);
        added.EquipmentId.Should().Be(Pump.Id);
        added.PlantUnitId.Should().Be(Pump.PlantUnitId);
        added.Priority.Should().Be(WorkOrderPriority.Critical);
        added.Status.Should().Be(WorkOrderStatus.Open);
    }

    [Fact]
    public async Task Details_the_domain_rejects_return_its_error_and_nothing_is_saved()
    {
        // Normally the validator stops this first; the handler must still not save what the aggregate refused.
        _equipment.Setup(r => r.GetByIdAsync(Pump.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Pump);
        var command = new CreateWorkOrderCommand("   ", "Dripping.", WorkOrderPriority.High, Pump.Id);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.TitleRequired);
        _workOrders.Verify(r => r.Add(It.IsAny<WorkOrder>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
