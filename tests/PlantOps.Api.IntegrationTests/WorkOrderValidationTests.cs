using System.Net;
using System.Net.Http.Json;
using PlantOps.Api.Contracts;
using PlantOps.Api.IntegrationTests.Infrastructure;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Api.IntegrationTests;

/// <summary>Invalid input is answered with 400/404 problem details the web app can map onto its form fields.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkOrderValidationTests(PlantOpsApiFactory factory) : IAsyncLifetime
{
    private readonly WorkOrderSteps _steps = new(factory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Invalid_create_is_a_validation_problem_keyed_by_camelCase_field_names()
    {
        var staff = await factory.CreateClientAsAsync(TestUsers.Staff);
        // Priority 0 is not a defined WorkOrderPriority. The API accepts enum numbers, so it reaches the validator.
        var request = new CreateWorkOrderRequest(Title: "", Description: "", Priority: 0, EquipmentId: Guid.Empty);

        var response = await staff.PostAsJsonAsync("/api/work-orders", request, TestJson.Options, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation.Failed");
        problem.Errors!.Keys.Should().BeEquivalentTo("title", "description", "priority", "equipmentId");
    }

    [Fact]
    public async Task Create_for_unknown_equipment_is_not_found()
    {
        var staff = await factory.CreateClientAsAsync(TestUsers.Staff);
        var unknownEquipmentId = new Guid("00000000-0000-0000-0000-0000000000ee");
        var request = new CreateWorkOrderRequest("Valve passing", "Valve does not seal when closed.", WorkOrderPriority.High, unknownEquipmentId);

        var response = await staff.PostAsJsonAsync("/api/work-orders", request, TestJson.Options, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Equipment.NotFound");
    }

    [Fact]
    public async Task Page_beyond_the_maximum_is_a_validation_problem()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Staff);

        var response = await client.GetAsync($"/api/work-orders?page={ListWorkOrdersQuery.MaxPage + 1}", Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation.Failed");
        problem.Errors!.Keys.Should().Equal("page");
    }

    [Fact]
    public async Task Maximum_page_at_the_largest_page_size_is_an_empty_page_rather_than_an_overflow()
    {
        await _steps.ReportAsync();
        var client = await factory.CreateClientAsAsync(TestUsers.Staff);

        var response = await client.GetAsync(
            $"/api/work-orders?page={ListWorkOrdersQuery.MaxPage}&pageSize={ListWorkOrdersQuery.MaxPageSize}", Ct);

        // (MaxPage - 1) * MaxPageSize rows are skipped, which must still fit in an int.
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WorkOrderSummaryDto>>(TestJson.Options, Ct);
        page!.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(1);
    }
}
