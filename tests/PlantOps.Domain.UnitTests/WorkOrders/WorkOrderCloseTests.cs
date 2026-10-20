using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Domain.UnitTests.WorkOrders;

public sealed class WorkOrderCloseTests
{
    [Fact]
    public void Close_by_the_assigned_technician_records_the_trimmed_notes_and_time()
    {
        var workOrder = TestWorkOrders.InProgress();

        var result = workOrder.Close(TestWorkOrders.TechnicianId, "  Replaced the mechanical seal.\n", TestWorkOrders.ClosedAt);

        result.IsSuccess.Should().BeTrue();
        workOrder.Status.Should().Be(WorkOrderStatus.Closed);
        workOrder.ResolutionNotes.Should().Be("Replaced the mechanical seal.");
        workOrder.ClosedAtUtc.Should().Be(TestWorkOrders.ClosedAt);
    }

    [Fact]
    public void Close_by_a_technician_who_is_not_assigned_is_forbidden()
    {
        var workOrder = TestWorkOrders.InProgress();

        var result = workOrder.Close(TestWorkOrders.OtherTechnicianId, TestWorkOrders.ResolutionNotes, TestWorkOrders.ClosedAt);

        result.Error.Should().Be(WorkOrderErrors.NotAssignedTechnician);
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        workOrder.Status.Should().Be(WorkOrderStatus.InProgress);
        workOrder.ResolutionNotes.Should().BeNull();
        workOrder.ClosedAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Close_without_resolution_notes_is_rejected(string? notes)
    {
        var workOrder = TestWorkOrders.InProgress();

        var result = workOrder.Close(TestWorkOrders.TechnicianId, notes!, TestWorkOrders.ClosedAt);

        result.Error.Should().Be(WorkOrderErrors.ResolutionNotesRequired);
        workOrder.Should().BeEquivalentTo(TestWorkOrders.InProgress(), options => options.Excluding(w => w.Id));
    }

    [Fact]
    public void Close_with_notes_longer_than_the_maximum_is_rejected()
    {
        var workOrder = TestWorkOrders.InProgress();
        var notes = new string('N', WorkOrder.ResolutionNotesMaxLength + 1);

        var result = workOrder.Close(TestWorkOrders.TechnicianId, notes, TestWorkOrders.ClosedAt);

        result.Error.Should().Be(WorkOrderErrors.ResolutionNotesRequired);
        workOrder.Should().BeEquivalentTo(TestWorkOrders.InProgress(), options => options.Excluding(w => w.Id));
    }

    [Fact]
    public void Close_with_notes_of_exactly_the_maximum_length_is_accepted_and_surrounding_spaces_do_not_count()
    {
        var workOrder = TestWorkOrders.InProgress();
        var notes = new string('N', WorkOrder.ResolutionNotesMaxLength);

        var result = workOrder.Close(TestWorkOrders.TechnicianId, $"  {notes}  ", TestWorkOrders.ClosedAt);

        result.IsSuccess.Should().BeTrue();
        workOrder.ResolutionNotes.Should().Be(notes);
    }
}
