using System.Net;
using System.Net.Http.Json;
using PlantOps.Api.Contracts;
using PlantOps.Api.IntegrationTests.Infrastructure;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Api.IntegrationTests;

/// <summary>PUT /api/work-orders/{id}: who may edit, when, and what happens to an edit based on stale data.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkOrderEditingTests(PlantOpsApiFactory factory) : IAsyncLifetime
{
    private const string EditedDescription = "Seal now spraying; pump switched to the spare.";

    private readonly WorkOrderSteps _steps = new(factory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Reporter_can_edit_their_own_open_work_order()
    {
        var id = await _steps.ReportAsync("Seal leaking", WorkOrderPriority.Medium);
        var version = (await _steps.GetAsync(id)).Version;

        var response = await EditAsync(TestUsers.Staff, id, "Seal leaking badly", version, WorkOrderPriority.High);

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var details = await _steps.GetAsync(id);
        details.Title.Should().Be("Seal leaking badly");
        details.Description.Should().Be(EditedDescription);
        details.Priority.Should().Be(WorkOrderPriority.High);
    }

    [Fact]
    public async Task Edit_is_recorded_in_the_history_with_the_old_and_new_values()
    {
        var id = await _steps.ReportAsync("Seal leaking", WorkOrderPriority.Medium);
        var version = (await _steps.GetAsync(id)).Version;
        await (await EditAsync(TestUsers.Staff, id, "Seal leaking badly", version, WorkOrderPriority.High))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var client = await factory.CreateClientAsAsync(TestUsers.Supervisor);

        var history = await client.GetFromJsonAsync<List<AuditEntryDto>>($"/api/work-orders/{id}/history", TestJson.Options, Ct);

        var edit = history!.Last();
        edit.Action.Should().Be("Updated");
        edit.UserName.Should().Be(TestUsers.Staff.DisplayName);
        edit.Changes.Select(c => (c.Property, Old: c.OldValue?.ToString(), New: c.NewValue?.ToString())).Should().BeEquivalentTo(
        [
            ("Title", "Seal leaking", "Seal leaking badly"),
            ("Description", WorkOrderSteps.DefaultDescription, EditedDescription),
            ("Priority", "Medium", "High"),
        ]);
    }

    [Fact]
    public async Task Reporter_cannot_edit_their_work_order_once_it_is_approved()
    {
        var id = await _steps.ReportAsync("Seal leaking");
        await _steps.ApproveAsync(id);
        var version = (await _steps.GetAsync(id)).Version;

        var response = await EditAsync(TestUsers.Staff, id, "Changed after approval", version);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "WorkOrder.EditNotAllowed");
        (await _steps.GetAsync(id)).Title.Should().Be("Seal leaking");
    }

    [Fact]
    public async Task Technician_cannot_edit_a_work_order_someone_else_reported()
    {
        var id = await _steps.ReportAsync("Seal leaking", reporter: TestUsers.Staff);
        var version = (await _steps.GetAsync(id)).Version;

        var response = await EditAsync(TestUsers.TechnicianA, id, "Not my report", version);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "WorkOrder.EditNotAllowed");
    }

    [Fact]
    public async Task Supervisor_can_edit_an_approved_work_order()
    {
        var id = await _steps.ReportAsync("Seal leaking");
        await _steps.ApproveAsync(id);
        var version = (await _steps.GetAsync(id)).Version;

        var response = await EditAsync(TestUsers.Supervisor, id, "Seal leaking, triaged", version, WorkOrderPriority.Critical);

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var details = await _steps.GetAsync(id);
        details.Title.Should().Be("Seal leaking, triaged");
        details.Priority.Should().Be(WorkOrderPriority.Critical);
    }

    [Fact]
    public async Task Edit_based_on_a_stale_version_is_a_conflict_and_keeps_the_newer_change()
    {
        var id = await _steps.ReportAsync("Seal leaking");
        var versionSeenByReporter = (await _steps.GetAsync(id)).Version;
        await (await EditAsync(TestUsers.Supervisor, id, "Edited by supervisor", versionSeenByReporter))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var response = await EditAsync(TestUsers.Staff, id, "Edited by reporter", versionSeenByReporter);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Concurrency.Conflict");
        (await _steps.GetAsync(id)).Title.Should().Be("Edited by supervisor");
    }

    [Fact]
    public async Task Closed_work_order_cannot_be_edited_even_by_a_supervisor()
    {
        var id = await _steps.ReportClosedByAsync(TestUsers.TechnicianA, "Seal leaking");
        var version = (await _steps.GetAsync(id)).Version;

        var response = await EditAsync(TestUsers.Supervisor, id, "Rewriting history", version);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "WorkOrder.ClosedCannotBeEdited");
        (await _steps.GetAsync(id)).Title.Should().Be("Seal leaking");
    }

    private async Task<HttpResponseMessage> EditAsync(
        TestUser user, Guid id, string title, byte[] version, WorkOrderPriority priority = WorkOrderPriority.Medium)
    {
        var client = await factory.CreateClientAsAsync(user);
        var request = new UpdateWorkOrderRequest(title, EditedDescription, priority, version);
        return await client.PutAsJsonAsync($"/api/work-orders/{id}", request, TestJson.Options, Ct);
    }
}
