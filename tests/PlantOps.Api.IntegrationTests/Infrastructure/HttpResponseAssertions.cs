using System.Net;
using System.Net.Http.Json;

namespace PlantOps.Api.IntegrationTests.Infrastructure;

public static class HttpResponseAssertions
{
    private const string ProblemJson = "application/problem+json";

    /// <summary>Asserts the status code. On failure the message includes the response body, which usually says why.</summary>
    public static async Task ShouldHaveStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(expected, "the response body was: {0}", body);
    }

    /// <summary>
    /// Asserts an RFC 7807 problem response (status code, problem+json content type and, when given, the "code"
    /// extension the web app switches on) and returns its body for any further checks.
    /// </summary>
    public static async Task<ApiProblem> ShouldBeProblemAsync(
        this HttpResponseMessage response, HttpStatusCode expectedStatus, string? expectedCode = null)
    {
        await response.ShouldHaveStatusAsync(expectedStatus);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemJson);

        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(TestJson.Options, TestContext.Current.CancellationToken);
        problem!.Status.Should().Be((int)expectedStatus);
        if (expectedCode is not null)
        {
            problem.Code.Should().Be(expectedCode);
        }

        return problem;
    }
}
