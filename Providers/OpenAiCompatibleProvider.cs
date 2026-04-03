using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AIConsoleApp.Infrastructure;
using AIConsoleApp.Models;
using AIConsoleApp.Services;

namespace AIConsoleApp.Providers;

public abstract class OpenAiCompatibleProvider : AiProvider
{
    private readonly string _baseUrl;

    protected OpenAiCompatibleProvider(
        string providerName,
        string model,
        KeyManager keyManager,
        HttpClient httpClient,
        ProviderRuntimeOptions runtimeOptions,
        IAppLogger logger,
        string baseUrl)
        : base(providerName, model, keyManager, httpClient, runtimeOptions, logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public override Task<string> SendMessageAsync(string message, List<ChatMessage> history, CancellationToken ct)
    {
        return WithKeyFallbackAsync((key, token) => SendInternalAsync(key, message, history, token), ct);
    }

    public override IAsyncEnumerable<string> StreamMessageAsync(string message, List<ChatMessage> history, CancellationToken ct)
    {
        return WithKeyFallbackStreamAsync((key, token) => StreamInternalAsync(key, message, history, token), ct);
    }

    public override async Task<IReadOnlyList<string>> DiscoverModelsAsync(CancellationToken ct)
    {
        try
        {
            return await WithKeyFallbackAsync(DiscoverInternalAsync, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Logger.WarningAsync($"provider={ProviderName} mode=models status=skipped error={ex.Message}", ct).ConfigureAwait(false);
            return Array.Empty<string>();
        }
    }

    protected virtual object BuildRequestPayload(string message, List<ChatMessage> history, bool stream)
    {
        return new
        {
            model = Model,
            messages = BuildMessages(history, message),
            stream
        };
    }

    protected virtual string ParseResponse(JsonElement root)
    {
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
            {
                return JsonTextExtractor.ExtractText(content);
            }

            if (first.TryGetProperty("text", out var text))
            {
                return text.GetString() ?? string.Empty;
            }
        }

        return JsonTextExtractor.ExtractText(root);
    }

    protected virtual string? ParseStreamChunk(JsonElement root)
    {
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("delta", out var delta))
            {
                if (delta.TryGetProperty("content", out var content))
                {
                    return JsonTextExtractor.ExtractText(content);
                }

                if (delta.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }

            if (first.TryGetProperty("text", out var textValue))
            {
                return textValue.GetString();
            }
        }

        return null;
    }

    private async Task<string> SendInternalAsync(string? key, string message, List<ChatMessage> history, CancellationToken ct)
    {
        using var request = BuildRequest(key, message, history, stream: false);
        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw CreateApiException(response.StatusCode, body);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: ct).ConfigureAwait(false);
        return ParseResponse(document.RootElement);
    }

    private async IAsyncEnumerable<string> StreamInternalAsync(string? key, string message, List<ChatMessage> history, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var request = BuildRequest(key, message, history, stream: true);
        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw CreateApiException(response.StatusCode, body);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await foreach (var sseEvent in SseReader.ReadEventsAsync(responseStream, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(sseEvent.Data) || string.Equals(sseEvent.Data, "[DONE]", StringComparison.Ordinal))
            {
                continue;
            }

            using var document = JsonDocument.Parse(sseEvent.Data);
            var chunk = ParseStreamChunk(document.RootElement);
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }
    }

    private async Task<IReadOnlyList<string>> DiscoverInternalAsync(string? key, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_baseUrl}/models"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.MethodNotAllowed)
        {
            return Array.Empty<string>();
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw CreateApiException(response.StatusCode, body);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return data.EnumerateArray()
            .Select(static item => item.TryGetProperty("id", out var id) ? id.GetString() : null)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private HttpRequestMessage BuildRequest(string? key, string message, List<ChatMessage> history, bool stream)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{_baseUrl}/chat/completions"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(stream ? "text/event-stream" : "application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(BuildRequestPayload(message, history, stream));
        return request;
    }

    protected static List<object> BuildMessages(List<ChatMessage> history, string currentMessage)
    {
        var messages = history
            .Select(static item => new
            {
                role = NormalizeRole(item.Role),
                content = item.Content
            })
            .Cast<object>()
            .ToList();

        messages.Add(new
        {
            role = "user",
            content = currentMessage
        });

        return messages;
    }

    protected static string NormalizeRole(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "assistant" => "assistant",
            "system" => "system",
            _ => "user"
        };
    }
}
