using System.Net;
using System.Text;
using System.Text.Json;

namespace PlantOps.Api.IntegrationTests.Infrastructure;

/// <summary>Answers HttpClient requests from a function instead of the network, and records each request and its body.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    /// <summary>A chat-completions response whose only choice says <paramref name="content"/>.</summary>
    public static HttpResponseMessage Completion(string? content, string finishReason = "stop") =>
        Json(JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { role = "assistant", content }, finish_reason = finishReason } },
            usage = new { prompt_tokens = 420, completion_tokens = 96 },
        }));

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return respond(request);
    }
}
