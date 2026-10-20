using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using PlantOps.Api.Contracts;
using PlantOps.Api.IntegrationTests.Infrastructure;
using PlantOps.Application.Abstractions.Authentication;
using PlantOps.Application.Auth;

namespace PlantOps.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthenticationTests(PlantOpsApiFactory factory) : IAsyncLifetime
{
    private const string WrongPassword = "Not-The-Password-1!";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Login_returns_a_one_hour_access_token_that_carries_the_users_role()
    {
        var client = factory.CreateClient();

        var response = await LoginAsync(client, TestUsers.Supervisor.Email, TestUsers.Password);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<AccessTokenDto>(TestJson.Options, Ct);
        token!.ExpiresInSeconds.Should().Be(3600);
        token.User.Id.Should().Be(TestUsers.Supervisor.Id);
        token.User.Roles.Should().Equal(Roles.Supervisor);
        // The token itself must carry the role, not just the response body: listing users needs Supervisor or Admin.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        await (await client.GetAsync("/api/users", Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_identical_401()
    {
        var email = await CreateAccountAsync("wrong.password@tests.plantops");
        var client = factory.CreateClient();

        var wrongPassword = await LoginAsync(client, email, WrongPassword);
        var unknownEmail = await LoginAsync(client, "nobody@tests.plantops", TestUsers.Password);

        await wrongPassword.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        await unknownEmail.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        // The whole body, not just the fields ApiProblem reads, so no extra field can hint that the account exists.
        (await BodyWithoutTraceIdAsync(unknownEmail)).Should().Be(
            await BodyWithoutTraceIdAsync(wrongPassword), "the response must not reveal which emails have accounts");
    }

    [Fact]
    public async Task Request_without_a_token_is_unauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/work-orders", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_with_a_tampered_payload_is_unauthorized()
    {
        var token = await GetAccessTokenAsync(TestUsers.Staff);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WithRole(token, Roles.Admin));

        var response = await client.GetAsync("/api/users", Ct);

        // 401, not 403: with the signature no longer matching, the token is not trusted at all, so the forged
        // Admin role never reaches authorization.
        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle()
            .Which.Parameter.Should().Contain("invalid_token").And.Contain("signature");
    }

    [Fact]
    public async Task Four_wrong_passwords_do_not_lock_the_account()
    {
        var email = await CreateAccountAsync("four.failures@tests.plantops");
        var client = factory.CreateClient();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await (await LoginAsync(client, email, WrongPassword)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
        }

        var response = await LoginAsync(client, email, TestUsers.Password);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_so_even_the_right_password_is_refused()
    {
        var email = await CreateAccountAsync("lockout@tests.plantops");
        var client = factory.CreateClient();
        await (await LoginAsync(client, email, TestUsers.Password)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await (await LoginAsync(client, email, WrongPassword)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
        }

        var response = await LoginAsync(client, email, TestUsers.Password);

        // Same code as a wrong password: a locked account is not revealed either.
        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), TestJson.Options, Ct);

    // traceId differs on every response by design; everything else must match.
    private static async Task<string> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!.AsObject();
        body.Remove("traceId");
        return body.ToJsonString();
    }

    private async Task<string> GetAccessTokenAsync(TestUser user)
    {
        var response = await LoginAsync(factory.CreateClient(), user.Email, TestUsers.Password);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<AccessTokenDto>(TestJson.Options, Ct);
        return token!.AccessToken;
    }

    // Each test that sends wrong passwords gets its own account, so failed attempts never add up to locking one of
    // the shared test users that every other test signs in as.
    private async Task<string> CreateAccountAsync(string email)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var request = new CreateUserRequest(email, "Login Test", TestUsers.Password, Roles.Staff);

        var response = await admin.PostAsJsonAsync("/api/users", request, TestJson.Options, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        return email;
    }

    // Edits the role claim but keeps the original signature, as an attacker editing the token by hand would.
    private static string WithRole(string token, string role)
    {
        var parts = token.Split('.');
        var payload = JsonNode.Parse(Base64Url.DecodeFromChars(parts[1]))!;
        payload["role"] = role;
        parts[1] = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        return string.Join('.', parts);
    }
}
