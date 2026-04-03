using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AIConsoleApp.Infrastructure;
using AIConsoleApp.Models;
using AIConsoleApp.Services;

namespace AIConsoleApp.Providers;

public sealed class CohereProvider : AiProvider
{
    private const string Endpoint = "https://api.cohere.com/v2/chat";

    public CohereProvider(string model, KeyManager keyManager, HttpClient httpClient, ProviderRuntimeOptions runtimeOptions, IAppLogger logger)
        : base("cohere", model, keyManager, httpClient, runtimeOptions, logger)
    {
    }

    public override Task<string> SendMessageAsync(string message, List<ChatMessage> history, CancellationToken ct)
    {
        return WithKeyFallbackAsync((key, token) => SendInternalAsync(key, message, PrepareHistory(history), token), ct);
    }

    public override IAsyncEnumerable<string> StreamMessageAsync(string message, List<ChatMessage> history, CancellationToken ct)
    {
        return WithKeyFallbackStreamAsync((key, token) => StreamInternalAsync(key, message, PrepareHistory(history), token), ct);
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
        return ExtractResponseText(document.RootElement);
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
            if (string.IsNullOrWhiteSpace(sseEvent.Data))
            {
                continue;
            }

            using var document = JsonDocument.Parse(sseEvent.Data);
            var chunk = ExtractStreamText(document.RootElement);
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }
    }

    private HttpRequestMessage BuildRequest(string? key, string message, List<ChatMessage> history, bool stream)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(stream ? "text/event-stream" : "application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(new
        {
            model = Model,
            messages = BuildMessages(history, message),
            stream
        });
        return request;
    }

    private static string ExtractResponseText(JsonElement root)
    {
        if (root.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content))
        {
            return JsonTextExtractor.ExtractText(content);
        }

        if (root.TryGetProperty("text", out var text))
        {
            return text.GetString() ?? string.Empty;
        }

        return JsonTextExtractor.ExtractText(root);
    }

    private static string? ExtractStreamText(JsonElement root)
    {
        if (root.TryGetProperty("type", out var typeElement))
        {
            var type = typeElement.GetString();
            if (string.Equals(type, "content-delta", StringComparison.OrdinalIgnoreCase)
                && root.TryGetProperty("delta", out var delta))
            {
                return JsonTextExtractor.ExtractText(delta);
            }
        }

        return null;
    }

    private static List<object> BuildMessages(List<ChatMessage> history, string currentMessage)
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

    private static string NormalizeRole(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "assistant" => "assistant",
            "system" => "system",
            _ => "user"
        };
    }
}

