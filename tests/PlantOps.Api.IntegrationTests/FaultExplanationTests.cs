using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlantOps.Api.IntegrationTests.Infrastructure;
using PlantOps.Application.Abstractions.Ai;
using PlantOps.Application.Features;
using PlantOps.Application.WorkOrders.Queries;

namespace PlantOps.Api.IntegrationTests;

/// <summary>
/// POST /api/work-orders/{id}/explanation and GET /api/features over HTTP. The AI model is replaced by a fake (or, in
/// the last test, by a stub HTTP handler behind the real adapter), so nothing here needs a network or a model.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FaultExplanationTests(PlantOpsApiFactory factory) : IAsyncLifetime
{
    private readonly WorkOrderSteps _steps = new(factory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task By_default_the_feature_is_off_and_the_endpoint_answers_404()
    {
        var id = await _steps.ReportAsync();
        var client = await factory.CreateClientAsAsync(TestUsers.TechnicianA);

        var features = await client.GetFromJsonAsync<FeaturesDto>("/api/features", TestJson.Options, Ct);
        var response = await client.PostAsync($"/api/work-orders/{id}/explanation", content: null, Ct);

        features!.ExplainFault.Should().BeFalse();
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "FaultExplanation.Disabled");
    }

    [Fact]
    public async Task When_switched_on_it_explains_the_work_orders_fault_and_says_which_model_answered()
    {
        var explainer = new FakeFaultExplainer();
        await using var api = ApiWith(explainer);
        var id = await _steps.ReportAsync("Mechanical seal leaking");
        var client = await factory.CreateClientAsAsync(api, TestUsers.TechnicianA);

        var features = await client.GetFromJsonAsync<FeaturesDto>("/api/features", TestJson.Options, Ct);
        var response = await client.PostAsync($"/api/work-orders/{id}/explanation", content: null, Ct);

        features!.ExplainFault.Should().BeTrue();
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var explanation = await response.Content.ReadFromJsonAsync<FaultExplanationDto>(TestJson.Options, Ct);
        explanation.Should().Be(explainer.Answer.Value);
        explainer.LastReport!.Title.Should().Be("Mechanical seal leaking");
        explainer.LastReport.Description.Should().Be(WorkOrderSteps.DefaultDescription);
    }

    [Fact]
    public async Task Explaining_a_work_order_that_does_not_exist_is_404()
    {
        await using var api = ApiWith(new FakeFaultExplainer());
        var client = await factory.CreateClientAsAsync(api, TestUsers.Supervisor);

        var response = await client.PostAsync($"/api/work-orders/{Guid.NewGuid()}/explanation", content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "WorkOrder.NotFound");
    }

    [Fact]
    public async Task A_model_that_does_not_answer_is_a_503_problem_never_a_401()
    {
        // The web app signs the user out on any 401, so a provider rejecting its own key must not look like one.
        await using var api = ApiWith(new FakeFaultExplainer { Answer = FaultExplanationErrors.Unavailable });
        var id = await _steps.ReportAsync();
        var client = await factory.CreateClientAsAsync(api, TestUsers.Supervisor);

        var response = await client.PostAsync($"/api/work-orders/{id}/explanation", content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "FaultExplanation.Unavailable");
    }

    [Fact]
    public async Task Each_user_has_their_own_explain_limit()
    {
        await using var api = ApiWith(new FakeFaultExplainer(), explainPermitLimit: 1);
        var id = await _steps.ReportAsync();
        var supervisor = await factory.CreateClientAsAsync(api, TestUsers.Supervisor);
        var technician = await factory.CreateClientAsAsync(api, TestUsers.TechnicianA);

        await (await supervisor.PostAsync($"/api/work-orders/{id}/explanation", null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await supervisor.PostAsync($"/api/work-orders/{id}/explanation", null, Ct)).ShouldBeProblemAsync(HttpStatusCode.TooManyRequests);
        await (await technician.PostAsync($"/api/work-orders/{id}/explanation", null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task With_the_real_adapter_the_API_calls_chat_completions_under_the_configured_base_URL()
    {
        var provider = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Completion("Safety:\n- Isolate first"));
        await using var api = ApiWithRealAdapter(provider);
        var id = await _steps.ReportAsync();
        var client = await factory.CreateClientAsAsync(api, TestUsers.TechnicianA);

        var response = await client.PostAsync($"/api/work-orders/{id}/explanation", content: null, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var explanation = await response.Content.ReadFromJsonAsync<FaultExplanationDto>(TestJson.Options, Ct);
        explanation!.Model.Should().Be("test-model");
        var (request, _) = provider.Requests.Should().ContainSingle().Subject;
        request.RequestUri.Should().Be(new Uri("http://ai.test/v1/chat/completions"));
        request.Headers.Authorization!.ToString().Should().Be("Bearer test-key");
    }

    [Fact]
    public async Task An_answer_bigger_than_the_response_cap_is_a_503_not_a_500()
    {
        // A wrong or misbehaving server behind Ai:BaseUrl: 300 KB, over the typed client's 256 KB cap.
        var provider = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Completion(new string('x', 300 * 1024)));
        await using var api = ApiWithRealAdapter(provider);
        var id = await _steps.ReportAsync();
        var client = await factory.CreateClientAsAsync(api, TestUsers.TechnicianA);

        var response = await client.PostAsync($"/api/work-orders/{id}/explanation", content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "FaultExplanation.Unavailable");
    }

    private WebApplicationFactory<Program> ApiWith(IFaultExplainer explainer, int explainPermitLimit = 1_000) =>
        factory.WithWebHostBuilder(builder => builder
            .UseSetting("RateLimiting:ExplainPermitLimit", explainPermitLimit.ToString(CultureInfo.InvariantCulture))
            .ConfigureTestServices(services => services.AddSingleton(explainer)));

    // The real ChatCompletionsFaultExplainer and its typed-client registration, with the network replaced by a stub.
    // No trailing slash on the base URL on purpose: the registration adds it, or "v1" would be dropped from the URL.
    private WebApplicationFactory<Program> ApiWithRealAdapter(StubHttpMessageHandler provider) =>
        factory.WithWebHostBuilder(builder => builder
            .UseSetting("Ai:Enabled", "true")
            .UseSetting("Ai:BaseUrl", "http://ai.test/v1")
            .UseSetting("Ai:Model", "test-model")
            .UseSetting("Ai:ApiKey", "test-key")
            .ConfigureTestServices(services =>
                services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => provider))));
}
