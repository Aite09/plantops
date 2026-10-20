using System.Net;
using System.Net.Http.Json;
using PlantOps.Api.Contracts;
using PlantOps.Application.PlantUnits;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Moves work orders through their lifecycle via the real API, each step as a user who is allowed to take it, so a
/// test only spells out the step it is about. Every step asserts that it succeeded, so broken setup fails loudly
/// at the step that broke instead of as a confusing assertion later.
/// </summary>
public sealed class WorkOrderSteps(Func<TestUser, Task<HttpClient>> createClientAsAsync)
{
    public const string DefaultTitle = "Mechanical seal leaking";
    public const string DefaultDescription = "Visible drip from the mechanical seal on the drive end.";
    public const string DefaultResolutionNotes = "Replaced the mechanical seal and checked for leaks.";

    private Guid? _equipmentId;

    /// <summary>Steps sent to the shared API.</summary>
    public WorkOrderSteps(PlantOpsApiFactory factory)
        : this(factory.CreateClientAsAsync)
    {
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A piece of equipment to report against, picked from GET /api/plant-units like the web app does.</summary>
    public async Task<Guid> GetEquipmentIdAsync()
    {
        if (_equipmentId is { } cached)
        {
            return cached;
        }

        var client = await createClientAsAsync(TestUsers.Staff);
        var units = await client.GetFromJsonAsync<List<PlantUnitDto>>("/api/plant-units", TestJson.Options, Ct);
        _equipmentId = units!.SelectMany(u => u.Equipment).First().Id;
        return _equipmentId.Value;
    }

    /// <summary>Reports a fault (Open) and returns the new work order's id.</summary>
    public async Task<Guid> ReportAsync(
        string title = DefaultTitle, WorkOrderPriority priority = WorkOrderPriority.Medium, TestUser? reporter = null)
    {
        var client = await createClientAsAsync(reporter ?? TestUsers.Staff);
        var request = new CreateWorkOrderRequest(title, DefaultDescription, priority, await GetEquipmentIdAsync());

        var response = await client.PostAsJsonAsync("/api/work-orders", request, TestJson.Options, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(TestJson.Options, Ct);
        return created!.Id;
    }

    public Task ApproveAsync(Guid id) =>
        PostAsync(TestUsers.Supervisor, $"/api/work-orders/{id}/approve");

    public Task AssignAsync(Guid id, TestUser technician) =>
        PostAsync(TestUsers.Supervisor, $"/api/work-orders/{id}/assign", new AssignWorkOrderRequest(technician.Id));

    public Task StartAsync(Guid id, TestUser technician) =>
        PostAsync(technician, $"/api/work-orders/{id}/start");

    public Task CloseAsync(Guid id, TestUser technician, string resolutionNotes = DefaultResolutionNotes) =>
        PostAsync(technician, $"/api/work-orders/{id}/close", new CloseWorkOrderRequest(resolutionNotes));

    /// <summary>Reported, approved and assigned to the technician, ready for them to start.</summary>
    public async Task<Guid> ReportAssignedToAsync(
        TestUser technician, string title = DefaultTitle, WorkOrderPriority priority = WorkOrderPriority.Medium)
    {
        var id = await ReportAsync(title, priority);
        await ApproveAsync(id);
        await AssignAsync(id, technician);
        return id;
    }

    /// <summary>The whole lifecycle: reported, approved, assigned, then started and closed by the technician.</summary>
    public async Task<Guid> ReportClosedByAsync(TestUser technician, string title = DefaultTitle)
    {
        var id = await ReportAssignedToAsync(technician, title);
        await StartAsync(id, technician);
        await CloseAsync(id, technician);
        return id;
    }

    public async Task<WorkOrderDetailsDto> GetAsync(Guid id)
    {
        var client = await createClientAsAsync(TestUsers.Staff);
        var details = await client.GetFromJsonAsync<WorkOrderDetailsDto>($"/api/work-orders/{id}", TestJson.Options, Ct);
        return details!;
    }

    private async Task PostAsync(TestUser user, string url, object? body = null)
    {
        var client = await createClientAsAsync(user);
        var response = body is null
            ? await client.PostAsync(url, content: null, Ct)
            : await client.PostAsJsonAsync(url, body, TestJson.Options, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }
}
