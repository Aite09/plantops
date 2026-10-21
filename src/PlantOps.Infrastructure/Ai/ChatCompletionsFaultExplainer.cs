using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlantOps.Application.Abstractions.Ai;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.Common;

namespace PlantOps.Infrastructure.Ai;

/// <summary>
/// Adapter for the OpenAI-compatible chat-completions API (POST {BaseUrl}chat/completions), which Ollama and most
/// hosted providers implement: one typed HttpClient and System.Text.Json, no SDK. No retries: a retry doubles the wait
/// on a slow local model and spends a hosted provider's rate limit; the user can press the button again. Every provider
/// failure becomes FaultExplanationErrors.Unavailable (503). Prompt and answer text are never logged.
/// </summary>
internal sealed class ChatCompletionsFaultExplainer(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    TimeProvider timeProvider,
    ILogger<ChatCompletionsFaultExplainer> logger) : IFaultExplainer
{
    // Low: steady, factual answers rather than creative ones. Not 0, which some providers treat specially.
    private const double Temperature = 0.2;

    public bool IsEnabled => options.Value.Enabled;

    public async Task<Result<FaultExplanationDto>> ExplainAsync(FaultReport report, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return FaultExplanationErrors.Disabled;
        }

        var settings = options.Value;
        var request = new ChatRequest(
            settings.Model,
            [
                new ChatMessage("system", FaultExplanationPrompt.System),
                new ChatMessage("user", FaultExplanationPrompt.BuildUserMessage(report)),
            ],
            Temperature,
            settings.MaxTokens,
            Stream: false);
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var response = await httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // The provider's error body is neither read nor passed on: it can echo the prompt back.
                logger.LogWarning("AI provider answered {StatusCode} for model {Model}", (int)response.StatusCode, settings.Model);
                return FaultExplanationErrors.Unavailable;
            }

            // Read as UTF-8 JSON whatever charset the provider claims: ReadFromJsonAsync would throw an
            // InvalidOperationException (a 500) for a charset .NET doesn't know.
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var body = await JsonSerializer.DeserializeAsync<ChatResponse>(stream, JsonSerializerOptions.Web, cancellationToken);
            var choice = body?.Choices is [var first, ..] ? first : null;
            var text = FaultExplanationPrompt.CleanAnswer(choice?.Message?.Content);
            if (text.Length == 0)
            {
                logger.LogWarning("AI provider returned no text for model {Model}", settings.Model);
                return FaultExplanationErrors.Unavailable;
            }

            logger.LogInformation(
                "AI explanation from {Model} in {ElapsedMs:0} ms: {PromptTokens} prompt and {CompletionTokens} completion tokens, finish reason {FinishReason}",
                settings.Model,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                body?.Usage?.PromptTokens,
                body?.Usage?.CompletionTokens,
                choice?.FinishReason);

            // "length": the model hit max_tokens mid-answer. Say so rather than present a shortened answer as complete.
            if (choice?.FinishReason == "length")
            {
                text = $"{text}\n\n{FaultExplanationPrompt.CutShortNote}";
            }

            return new FaultExplanationDto(text, settings.Model, timeProvider.GetUtcNow());
        }
        catch (HttpRequestException exception)
        {
            // Unreachable, or an answer bigger than the client's MaxResponseContentBufferSize.
            logger.LogWarning("AI provider unreachable for model {Model}: {Reason}", settings.Model, exception.HttpRequestError);
            return FaultExplanationErrors.Unavailable;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed. If the caller hung up instead, the exception goes on up as usual.
            logger.LogWarning("AI provider timed out after {TimeoutSeconds} s for model {Model}", settings.TimeoutSeconds, settings.Model);
            return FaultExplanationErrors.Unavailable;
        }
        catch (JsonException)
        {
            logger.LogWarning("AI provider's answer was not chat-completions JSON for model {Model}", settings.Model);
            return FaultExplanationErrors.Unavailable;
        }
    }

    // The few fields of the chat-completions JSON this adapter uses. Web defaults give camelCase names; only the
    // snake_case ones need spelling out. Token counts are only logged, so a provider that sends null for them is fine.
    private sealed record ChatRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        bool Stream);

    private sealed record ChatMessage(string Role, string? Content);

    private sealed record ChatResponse(IReadOnlyList<ChatChoice>? Choices, ChatUsage? Usage);

    private sealed record ChatChoice(ChatMessage? Message, [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record ChatUsage(
        [property: JsonPropertyName("prompt_tokens")] int? PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int? CompletionTokens);
}
