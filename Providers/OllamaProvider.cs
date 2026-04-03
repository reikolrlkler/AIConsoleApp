using System.Net.Http.Json;
using System.Text.Json;
using AIConsoleApp.Infrastructure;
using AIConsoleApp.Models;
using AIConsoleApp.Services;

namespace AIConsoleApp.Providers;

public sealed class OllamaProvider : AiProvider
{
    private readonly string _baseUrl;

    public OllamaProvider(string model, KeyManager keyManager, HttpClient httpClient, ProviderRuntimeOptions runtimeOptions, IAppLogger logger)
        : base("ollama", model, keyManager, httpClient, runtimeOptions, logger, requiresApiKey: false)
    {
        _baseUrl = (Environment.GetEnvironmentVariable("AICONSOLE_OLLAMA_BASE_URL") ?? "http://localhost:11434").TrimEnd('/');
    }

    public override Task<string> SendMessageAsync(string message, List<ChatMessage> history, CancellationToken ct)
    {
        return WithKeyFallbackAsync((_, token) => SendInternalAsync(message, history, token), ct);
    }

    public override IAsyncEnumerable<string> StreamMessageAsync(string message, List<ChatMessage> history, CancellationToken ct)
    {
        return WithKeyFallbackStreamAsync((_, token) => StreamInternalAsync(message, history, token), ct);
    }

    public override Task<IReadOnlyList<string>> DiscoverModelsAsync(CancellationToken ct)
    {
        return GetInstalledModelsAsync(HttpClient, ct);
    }

    public static async Task<IReadOnlyList<string>> GetInstalledModelsAsync(HttpClient httpClient, CancellationToken ct)
    {
        var baseUrl = (Environment.GetEnvironmentVariable("AICONSOLE_OLLAMA_BASE_URL") ?? "http://localhost:11434").TrimEnd('/');
        using var response = await httpClient.GetAsync($"{baseUrl}/api/tags", ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<string>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return models.EnumerateArray()
            .Select(model => model.TryGetProperty("name", out var name) ? name.GetString() : null)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<string> SendInternalAsync(string message, List<ChatMessage> history, CancellationToken ct)
    {
        using var request = BuildRequest(message, history, stream: false);
        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw CreateApiException(response.StatusCode, body);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: ct).ConfigureAwait(false);
        if (document.RootElement.TryGetProperty("message", out var messageElement)
            && messageElement.TryGetProperty("content", out var content))
        {
            return JsonTextExtractor.ExtractText(content);
        }

        return JsonTextExtractor.ExtractText(document.RootElement);
    }

    private async IAsyncEnumerable<string> StreamInternalAsync(string message, List<ChatMessage> history, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var request = BuildRequest(message, history, stream: true);
        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw CreateApiException(response.StatusCode, body);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(responseStream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("message", out var messageElement)
                && messageElement.TryGetProperty("content", out var content))
            {
                var chunk = JsonTextExtractor.ExtractText(content);
                if (!string.IsNullOrEmpty(chunk))
                {
                    yield return chunk;
                }
            }
        }
    }

    private HttpRequestMessage BuildRequest(string message, List<ChatMessage> history, bool stream)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{_baseUrl}/api/chat"));
        request.Content = JsonContent.Create(new
        {
            model = Model,
            messages = BuildMessages(history, message),
            stream
        });
        return request;
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
