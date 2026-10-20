using Moq;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.WorkOrders.Commands;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Commands;

public sealed class UpdateWorkOrderCommandHandlerTests
{
    private static readonly Guid ReporterId = Guid.NewGuid();
    private static readonly byte[] Version = [0, 0, 0, 0, 0, 0, 7, 209];

    private readonly Mock<IWorkOrderRepository> _workOrders = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly UpdateWorkOrderCommandHandler _handler;

    public UpdateWorkOrderCommandHandlerTests() =>
        _handler = new UpdateWorkOrderCommandHandler(_workOrders.Object, _unitOfWork.Object, _currentUser.Object);

    [Fact]
    public async Task Reporter_can_edit_their_own_work_order_while_it_is_open()
    {
        var workOrder = Stored(TestWorkOrders.Open(ReporterId));
        ActAs(ReporterId, Roles.Staff);

        var result = await _handler.Handle(EditOf(workOrder), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        workOrder.Title.Should().Be("Seal leak on P-101A");
        workOrder.Description.Should().Be("Leak got worse overnight.");
        workOrder.Priority.Should().Be(WorkOrderPriority.Critical);
        _unitOfWork.Verify(u => u.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Reporter_is_forbidden_to_edit_once_the_work_order_is_approved()
    {
        var workOrder = Stored(TestWorkOrders.Approved(ReporterId));
        ActAs(ReporterId, Roles.Staff);

        var result = await _handler.Handle(EditOf(workOrder), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.EditNotAllowed);
        workOrder.Priority.Should().Be(WorkOrderPriority.High);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Staff_user_who_did_not_report_it_is_forbidden_to_edit_an_open_work_order()
    {
        var workOrder = Stored(TestWorkOrders.Open(ReporterId));
        ActAs(Guid.NewGuid(), Roles.Staff);

        var result = await _handler.Handle(EditOf(workOrder), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.EditNotAllowed);
        workOrder.Priority.Should().Be(WorkOrderPriority.High);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Admin)]
    public async Task Supervisor_or_admin_can_edit_a_work_order_after_it_is_approved(string role)
    {
        var workOrder = Stored(TestWorkOrders.Approved(ReporterId));
        ActAs(Guid.NewGuid(), role);

        var result = await _handler.Handle(EditOf(workOrder), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        workOrder.Priority.Should().Be(WorkOrderPriority.Critical);
        _unitOfWork.Verify(u => u.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Expected_version_is_set_to_the_commands_version_before_saving()
    {
        // Optimistic concurrency only works if the repository knows the client's version before SaveChanges runs.
        var workOrder = Stored(TestWorkOrders.Open(ReporterId));
        ActAs(ReporterId, Roles.Staff);
        var calls = new List<string>();
        _workOrders.Setup(r => r.SetExpectedVersion(workOrder, Version)).Callback(() => calls.Add("SetExpectedVersion"));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("SaveChanges"));

        await _handler.Handle(EditOf(workOrder), TestContext.Current.CancellationToken);

        calls.Should().Equal("SetExpectedVersion", "SaveChanges");
    }

    [Fact]
    public async Task Missing_work_order_returns_NotFound_and_nothing_is_saved()
    {
        var missingId = Guid.NewGuid();
        ActAs(Guid.NewGuid(), Roles.Supervisor);
        var command = new UpdateWorkOrderCommand(missingId, "Title", "Description", WorkOrderPriority.Low, Version);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.NotFound(missingId));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Closed_work_order_cannot_be_edited_even_by_a_supervisor()
    {
        var workOrder = Stored(TestWorkOrders.Closed(Guid.NewGuid()));
        ActAs(Guid.NewGuid(), Roles.Supervisor);

        var result = await _handler.Handle(EditOf(workOrder), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.ClosedCannotBeEdited);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private WorkOrder Stored(WorkOrder workOrder)
    {
        _workOrders.Setup(r => r.GetByIdAsync(workOrder.Id, It.IsAny<CancellationToken>())).ReturnsAsync(workOrder);
        return workOrder;
    }

    private void ActAs(Guid userId, string role)
    {
        _currentUser.Setup(u => u.UserId).Returns(userId);
        _currentUser.Setup(u => u.IsInRole(role)).Returns(true);
    }

    private static UpdateWorkOrderCommand EditOf(WorkOrder workOrder) =>
        new(workOrder.Id, "Seal leak on P-101A", "Leak got worse overnight.", WorkOrderPriority.Critical, Version);
}
