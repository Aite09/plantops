using System.Net;
using System.Net.Http.Json;
using PlantOps.Api.IntegrationTests.Infrastructure;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Api.IntegrationTests;

/// <summary>GET /api/dashboard: what it selects and counts against real SQL, who may see it, and its output cache.</summary>
[Collection(ApiCollection.Name)]
public sealed class DashboardTests(PlantOpsApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 16, 6, 0, 0, TimeSpan.Zero);

    private readonly WorkOrderSteps _steps = new(factory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Needs_attention_lists_waiting_Critical_and_High_work_and_ageing_work_highest_priority_first()
    {
        await using var api = new FakeClockApi(factory, Now.AddDays(-8));
        var ageing = await api.Steps.ReportAssignedToAsync(TestUsers.TechnicianA, "Low, assigned 8 days ago", WorkOrderPriority.Low);
        // Old enough to be ageing, but closed: closed work never needs attention.
        await api.Steps.ReportClosedByAsync(TestUsers.TechnicianA, "Closed 8 days ago");
        api.Clock.Advance(TimeSpan.FromDays(8));
        var unapproved = await api.Steps.ReportAsync("Critical, not approved", WorkOrderPriority.Critical);
        var unassigned = await api.Steps.ReportAsync("High, approved, not assigned", WorkOrderPriority.High);
        await api.Steps.ApproveAsync(unassigned);
        await api.Steps.ReportAsync("Medium, reported today", WorkOrderPriority.Medium);
        await api.Steps.ReportAssignedToAsync(TestUsers.TechnicianB, "High, already assigned", WorkOrderPriority.High);

        var dashboard = await GetAsync(await api.CreateClientAsAsync(TestUsers.Supervisor));

        dashboard.NeedsAttention.Select(i => i.Id).Should().Equal(unapproved, unassigned, ageing);
        dashboard.NeedsAttentionTotal.Should().Be(3);
        dashboard.NeedsAttention[2].AssignedToName.Should().Be(TestUsers.TechnicianA.DisplayName);
        dashboard.GeneratedAtUtc.Should().Be(Now);
        dashboard.AgeingAfterDays.Should().Be(7);
    }

    [Fact]
    public async Task Open_work_is_counted_per_unit_and_status_and_every_technician_is_listed_with_their_load()
    {
        var open = await _steps.ReportAsync();
        var approved = await _steps.ReportAsync();
        await _steps.ApproveAsync(approved);
        await _steps.ReportAssignedToAsync(TestUsers.TechnicianA);
        var started = await _steps.ReportAssignedToAsync(TestUsers.TechnicianA);
        await _steps.StartAsync(started, TestUsers.TechnicianA);
        await _steps.ReportClosedByAsync(TestUsers.TechnicianA);
        var unitId = (await _steps.GetAsync(open)).PlantUnitId;

        var dashboard = await GetAsync(await factory.CreateClientAsAsync(TestUsers.Supervisor));

        dashboard.Units.Should().ContainSingle(u => u.PlantUnitId == unitId)
            .Which.Should().BeEquivalentTo(new { Open = 1, Approved = 1, Assigned = 1, InProgress = 1 });
        // Units with no open work are listed too, with zeros: an empty row is information.
        dashboard.Units.Where(u => u.PlantUnitId != unitId).Should().NotBeEmpty()
            .And.OnlyContain(u => u.Open == 0 && u.Approved == 0 && u.Assigned == 0 && u.InProgress == 0);
        dashboard.Technicians.Should().ContainEquivalentOf(
            new TechnicianWorkloadDto(TestUsers.TechnicianA.Id, TestUsers.TechnicianA.DisplayName, 1, 1));
        dashboard.Technicians.Should().ContainEquivalentOf(
            new TechnicianWorkloadDto(TestUsers.TechnicianB.Id, TestUsers.TechnicianB.DisplayName, 0, 0));
    }

    [Fact]
    public async Task One_cached_copy_is_shared_by_supervisors_and_admins_until_a_work_order_changes()
    {
        await using var api = new FakeClockApi(factory, Now);
        var id = await api.Steps.ReportAsync("Critical, not approved", WorkOrderPriority.Critical);
        var supervisor = await api.CreateClientAsAsync(TestUsers.Supervisor);
        var admin = await api.CreateClientAsAsync(TestUsers.Admin);

        var first = await GetAsync(supervisor);
        // A freshly computed dashboard would now carry the later time; the cached copy keeps the first one.
        api.Clock.Advance(TimeSpan.FromMinutes(1));
        var response = await admin.GetAsync("/api/dashboard", Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var second = await response.Content.ReadFromJsonAsync<DashboardDto>(TestJson.Options, Ct);

        second!.GeneratedAtUtc.Should().Be(first.GeneratedAtUtc);
        response.Headers.Age.Should().NotBeNull("the output cache adds Age to responses it serves");

        await api.Steps.ApproveAsync(id);
        var afterChange = await GetAsync(supervisor);

        afterChange.GeneratedAtUtc.Should().Be(Now.AddMinutes(1));
        afterChange.NeedsAttention.Should().ContainSingle().Which.Status.Should().Be(WorkOrderStatus.Approved);
    }

    [Fact]
    public async Task Staff_technicians_and_signed_out_callers_are_refused_even_while_a_copy_is_cached()
    {
        await _steps.ReportAsync("Critical, not approved", WorkOrderPriority.Critical);
        await GetAsync(await factory.CreateClientAsAsync(TestUsers.Supervisor));

        foreach (var user in new[] { TestUsers.Staff, TestUsers.TechnicianA })
        {
            var client = await factory.CreateClientAsAsync(user);
            await (await client.GetAsync("/api/dashboard", Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden);
        }

        await (await factory.CreateClient().GetAsync("/api/dashboard", Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    private static async Task<DashboardDto> GetAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/dashboard", Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardDto>(TestJson.Options, Ct);
        return dashboard!;
    }
}
