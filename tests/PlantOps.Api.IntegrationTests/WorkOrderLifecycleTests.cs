using System.Net;
using System.Net.Http.Json;
using PlantOps.Api.Contracts;
using PlantOps.Api.IntegrationTests.Infrastructure;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class WorkOrderLifecycleTests(PlantOpsApiFactory factory) : IAsyncLifetime
{
    private readonly WorkOrderSteps _steps = new(factory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Work_order_goes_from_report_to_closed_and_the_details_show_who_did_each_step_and_when()
    {
        var reportedAt = new DateTimeOffset(2026, 3, 2, 6, 0, 0, TimeSpan.Zero);
        var approvedAt = reportedAt.AddMinutes(10);
        var assignedAt = approvedAt.AddMinutes(5);
        var startedAt = assignedAt.AddMinutes(30);
        var closedAt = startedAt.AddHours(2);
        await using var api = new FakeClockApi(factory, reportedAt);
        var id = await api.Steps.ReportAsync("P-101A seal leaking");
        api.Clock.SetUtcNow(approvedAt);
        await api.Steps.ApproveAsync(id);
        api.Clock.SetUtcNow(assignedAt);
        await api.Steps.AssignAsync(id, TestUsers.TechnicianA);
        api.Clock.SetUtcNow(startedAt);
        await api.Steps.StartAsync(id, TestUsers.TechnicianA);
        api.Clock.SetUtcNow(closedAt);
        await api.Steps.CloseAsync(id, TestUsers.TechnicianA, "Replaced the seal.");

        var details = await api.Steps.GetAsync(id);

        details.Status.Should().Be(WorkOrderStatus.Closed);
        details.ResolutionNotes.Should().Be("Replaced the seal.");
        details.ReportedByName.Should().Be(TestUsers.Staff.DisplayName);
        details.ApprovedByName.Should().Be(TestUsers.Supervisor.DisplayName);
        details.AssignedToName.Should().Be(TestUsers.TechnicianA.DisplayName);
        // Exact values, so a step that stamps the wrong field (or reuses another step's time) is caught.
        details.CreatedAtUtc.Should().Be(reportedAt);
        details.ApprovedAtUtc.Should().Be(approvedAt);
        details.AssignedAtUtc.Should().Be(assignedAt);
        details.StartedAtUtc.Should().Be(startedAt);
        details.ClosedAtUtc.Should().Be(closedAt);
    }

    [Fact]
    public async Task History_lists_every_change_with_the_acting_users_name_and_no_row_version()
    {
        var id = await _steps.ReportClosedByAsync(TestUsers.TechnicianA);
        var client = await factory.CreateClientAsAsync(TestUsers.Staff);

        var history = (await client.GetFromJsonAsync<List<AuditEntryDto>>($"/api/work-orders/{id}/history", TestJson.Options, Ct))!;

        history.Select(h => (h.Action, h.UserName, NewStatus: NewValueOf(h, "Status"))).Should().Equal(
            ("Created", TestUsers.Staff.DisplayName, "Open"),
            ("Updated", TestUsers.Supervisor.DisplayName, "Approved"),
            ("Updated", TestUsers.Supervisor.DisplayName, "Assigned"),
            ("Updated", TestUsers.TechnicianA.DisplayName, "InProgress"),
            ("Updated", TestUsers.TechnicianA.DisplayName, "Closed"));
        history.SelectMany(h => h.Changes).Should().NotContain(c => c.Property == "RowVersion");
    }

    [Fact]
    public async Task Start_by_a_technician_who_is_not_assigned_is_forbidden()
    {
        var id = await _steps.ReportAssignedToAsync(TestUsers.TechnicianA);
        var otherTechnician = await factory.CreateClientAsAsync(TestUsers.TechnicianB);

        var response = await otherTechnician.PostAsync($"/api/work-orders/{id}/start", content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "WorkOrder.NotAssignedTechnician");
        (await _steps.GetAsync(id)).Status.Should().Be(WorkOrderStatus.Assigned);
    }

    [Fact]
    public async Task Closing_work_that_was_never_started_is_an_invalid_transition()
    {
        var id = await _steps.ReportAssignedToAsync(TestUsers.TechnicianA);
        var technician = await factory.CreateClientAsAsync(TestUsers.TechnicianA);

        var response = await technician.PostAsJsonAsync(
            $"/api/work-orders/{id}/close", new CloseWorkOrderRequest("Fixed."), TestJson.Options, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "WorkOrder.InvalidTransition");
        problem.Detail.Should().Be("A work order cannot move from Assigned to Closed.");
    }

    [Fact]
    public async Task Handover_of_work_in_progress_returns_it_to_assigned_for_the_new_technician()
    {
        var id = await _steps.ReportAssignedToAsync(TestUsers.TechnicianA);
        await _steps.StartAsync(id, TestUsers.TechnicianA);

        await _steps.AssignAsync(id, TestUsers.TechnicianB);

        var details = await _steps.GetAsync(id);
        details.Status.Should().Be(WorkOrderStatus.Assigned);
        details.AssignedToUserId.Should().Be(TestUsers.TechnicianB.Id);
        details.AssignedToName.Should().Be(TestUsers.TechnicianB.DisplayName);
        details.StartedAtUtc.Should().BeNull("the new technician has not started yet");
    }

    [Fact]
    public async Task After_a_handover_only_the_new_technician_can_start_the_work()
    {
        var id = await _steps.ReportAssignedToAsync(TestUsers.TechnicianA);
        await _steps.StartAsync(id, TestUsers.TechnicianA);
        await _steps.AssignAsync(id, TestUsers.TechnicianB);
        var previousTechnician = await factory.CreateClientAsAsync(TestUsers.TechnicianA);
        var newTechnician = await factory.CreateClientAsAsync(TestUsers.TechnicianB);

        var byPrevious = await previousTechnician.PostAsync($"/api/work-orders/{id}/start", content: null, Ct);
        var byNew = await newTechnician.PostAsync($"/api/work-orders/{id}/start", content: null, Ct);

        await byPrevious.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "WorkOrder.NotAssignedTechnician");
        await byNew.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    private static string? NewValueOf(AuditEntryDto entry, string property) =>
        entry.Changes.SingleOrDefault(c => c.Property == property)?.NewValue?.GetString();
}
