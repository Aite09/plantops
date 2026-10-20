using System.Net;
using System.Net.Http.Json;
using PlantOps.Api.IntegrationTests.Infrastructure;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Api.IntegrationTests;

/// <summary>GET /api/work-orders: filters, the priority-then-oldest order and paging, against real SQL.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkOrderListTests(PlantOpsApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTimeOffset ReportingStartsAt = new(2026, 3, 2, 6, 0, 0, TimeSpan.Zero);

    private readonly WorkOrderSteps _steps = new(factory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Active_only_leaves_out_closed_work_orders()
    {
        await _steps.ReportClosedByAsync(TestUsers.TechnicianA, "Already fixed");
        var open = await _steps.ReportAsync("Still leaking");

        var page = await ListAsync("?activeOnly=true");

        page.Items.Select(i => i.Id).Should().Equal(open);
        page.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Assigned_to_filter_returns_only_that_technicians_work()
    {
        var forTechnicianA = await _steps.ReportAssignedToAsync(TestUsers.TechnicianA, "For Alex");
        await _steps.ReportAssignedToAsync(TestUsers.TechnicianB, "For Blair");
        await _steps.ReportAsync("Not assigned yet");

        var page = await ListAsync($"?assignedToUserId={TestUsers.TechnicianA.Id}");

        var item = page.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(forTechnicianA);
        item.AssignedToName.Should().Be(TestUsers.TechnicianA.DisplayName);
    }

    [Fact]
    public async Task List_is_ordered_by_priority_then_oldest_first()
    {
        await using var api = new FakeClockApi(factory, ReportingStartsAt);
        (string Title, WorkOrderPriority Priority)[] reports =
        [
            ("Low, reported first", WorkOrderPriority.Low),
            ("Critical, reported second", WorkOrderPriority.Critical),
            ("High, reported third", WorkOrderPriority.High),
            ("Critical, reported fourth", WorkOrderPriority.Critical),
        ];
        foreach (var (title, priority) in reports)
        {
            await api.Steps.ReportAsync(title, priority);
            api.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        // Reading does not use the clock, so the shared API can list what the fake-clock API wrote.
        var page = await ListAsync("");

        page.Items.Select(i => i.Title).Should().Equal(
            "Critical, reported second",
            "Critical, reported fourth",
            "High, reported third",
            "Low, reported first");
    }

    [Fact]
    public async Task Paging_reports_the_totals_and_splits_the_list_without_gaps_or_repeats()
    {
        // Same priority, one minute apart, so "oldest first" fixes one order to page through.
        await using var api = new FakeClockApi(factory, ReportingStartsAt);
        for (var n = 1; n <= 5; n++)
        {
            await api.Steps.ReportAsync($"Work order {n}");
            api.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        PagedResponse<WorkOrderSummaryDto>[] pages =
            [await ListAsync("?pageSize=2&page=1"), await ListAsync("?pageSize=2&page=2"), await ListAsync("?pageSize=2&page=3")];

        pages.SelectMany(p => p.Items).Select(i => i.Title).Should().Equal(
            "Work order 1", "Work order 2", "Work order 3", "Work order 4", "Work order 5");
        pages.Select(p => p.Page).Should().Equal(1, 2, 3);
        pages.Should().AllSatisfy(p =>
        {
            p.PageSize.Should().Be(2);
            p.TotalCount.Should().Be(5);
            p.TotalPages.Should().Be(3);
        });
    }

    private async Task<PagedResponse<WorkOrderSummaryDto>> ListAsync(string queryString)
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Supervisor);
        var response = await client.GetAsync($"/api/work-orders{queryString}", Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WorkOrderSummaryDto>>(TestJson.Options, Ct);
        return page!;
    }
}
