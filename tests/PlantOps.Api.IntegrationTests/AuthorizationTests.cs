using System.Net;
using System.Net.Http.Json;
using PlantOps.Api.Contracts;
using PlantOps.Api.IntegrationTests.Infrastructure;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Auth;
using PlantOps.Application.Users;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Api.IntegrationTests;

/// <summary>The role-based policies from the README's permission matrix, checked at the HTTP boundary.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationTests(PlantOpsApiFactory factory) : IAsyncLifetime
{
    private readonly WorkOrderSteps _steps = new(factory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Staff_cannot_approve_a_work_order()
    {
        var id = await _steps.ReportAsync();
        var staff = await factory.CreateClientAsAsync(TestUsers.Staff);

        var response = await staff.PostAsync($"/api/work-orders/{id}/approve", content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden);
        (await _steps.GetAsync(id)).Status.Should().Be(WorkOrderStatus.Open);
    }

    [Fact]
    public async Task Technician_cannot_approve_a_work_order()
    {
        var id = await _steps.ReportAsync();
        var technician = await factory.CreateClientAsAsync(TestUsers.TechnicianA);

        var response = await technician.PostAsync($"/api/work-orders/{id}/approve", content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden);
        (await _steps.GetAsync(id)).Status.Should().Be(WorkOrderStatus.Open);
    }

    [Fact]
    public async Task Staff_cannot_list_users()
    {
        var staff = await factory.CreateClientAsAsync(TestUsers.Staff);

        var response = await staff.GetAsync("/api/users", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Supervisor_can_list_the_technicians_to_assign_work_to()
    {
        var supervisor = await factory.CreateClientAsAsync(TestUsers.Supervisor);

        var users = await supervisor.GetFromJsonAsync<List<UserDto>>("/api/users?role=Technician", TestJson.Options, Ct);

        users.Should().Contain(u => u.Id == TestUsers.TechnicianA.Id)
            .And.Contain(u => u.Id == TestUsers.TechnicianB.Id)
            .And.OnlyContain(u => u.Roles.Contains(Roles.Technician));
    }

    [Fact]
    public async Task Supervisor_cannot_create_users()
    {
        var supervisor = await factory.CreateClientAsAsync(TestUsers.Supervisor);
        var request = new CreateUserRequest("made.by.supervisor@tests.plantops", "Not Allowed", TestUsers.Password, Roles.Admin);

        var response = await supervisor.PostAsJsonAsync("/api/users", request, TestJson.Options, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden);
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var users = await admin.GetFromJsonAsync<List<UserDto>>("/api/users", TestJson.Options, Ct);
        users.Should().NotContain(u => u.Email == request.Email);
    }

    [Fact]
    public async Task Admin_can_create_a_user_who_can_then_sign_in_with_that_role()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var request = new CreateUserRequest("new.supervisor@tests.plantops", "Nia Supervisor", TestUsers.Password, Roles.Supervisor);

        var response = await admin.PostAsJsonAsync("/api/users", request, TestJson.Options, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(TestJson.Options, Ct);
        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(request.Email, request.Password), TestJson.Options, Ct);
        await login.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var token = await login.Content.ReadFromJsonAsync<AccessTokenDto>(TestJson.Options, Ct);
        token!.User.Id.Should().Be(created!.Id);
        token.User.Roles.Should().Equal(Roles.Supervisor);
    }

    [Fact]
    public async Task Creating_a_user_with_an_email_already_in_use_is_a_conflict()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var request = new CreateUserRequest(TestUsers.Staff.Email, "Duplicate Staff", TestUsers.Password, Roles.Staff);

        var response = await admin.PostAsJsonAsync("/api/users", request, TestJson.Options, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "User.EmailInUse");
    }

    [Fact]
    public async Task Assigning_to_a_user_who_is_not_a_technician_is_rejected()
    {
        var id = await _steps.ReportAsync();
        await _steps.ApproveAsync(id);
        var supervisor = await factory.CreateClientAsAsync(TestUsers.Supervisor);

        var response = await supervisor.PostAsJsonAsync(
            $"/api/work-orders/{id}/assign", new AssignWorkOrderRequest(TestUsers.Staff.Id), TestJson.Options, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "User.NotATechnician");
        var details = await _steps.GetAsync(id);
        details.Status.Should().Be(WorkOrderStatus.Approved);
        details.AssignedToUserId.Should().BeNull();
    }
}
