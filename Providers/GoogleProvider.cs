using AIConsoleApp.Services;
using Google.GenAI;
using Google.GenAI.Types;
using AiChatMessage = AIConsoleApp.Models.ChatMessage;

namespace AIConsoleApp.Providers;

public sealed class GoogleProvider : AiProvider
{
    public GoogleProvider(string model, KeyManager keyManager, HttpClient httpClient, ProviderRuntimeOptions runtimeOptions, IAppLogger logger)
        : base("google", model, keyManager, httpClient, runtimeOptions, logger)
    {
    }

    public override Task<string> SendMessageAsync(string message, List<AiChatMessage> history, CancellationToken ct)
    {
        return WithKeyFallbackAsync((key, token) => SendInternalAsync(key, message, history, token), ct);
    }

    public override IAsyncEnumerable<string> StreamMessageAsync(string message, List<AiChatMessage> history, CancellationToken ct)
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

    private async Task<string> SendInternalAsync(string? key, string message, List<AiChatMessage> history, CancellationToken ct)
    {
        try
        {
            using var client = CreateApiClient(key!);
            var response = await client.Models.GenerateContentAsync(Model, BuildContents(history, message), null, ct).ConfigureAwait(false);
            return response?.Text ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateSdkException(ex);
        }
    }

    private async IAsyncEnumerable<string> StreamInternalAsync(string? key, string message, List<AiChatMessage> history, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        IAsyncEnumerable<GenerateContentResponse> stream;

        try
        {
            var client = CreateApiClient(key!);
            stream = client.Models.GenerateContentStreamAsync(Model, BuildContents(history, message), null, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateSdkException(ex);
        }

        var assembled = string.Empty;
        await foreach (var update in stream.WithCancellation(ct).ConfigureAwait(false))
        {
            var text = update?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if (text.StartsWith(assembled, StringComparison.Ordinal))
            {
                var delta = text[assembled.Length..];
                assembled = text;
                if (!string.IsNullOrEmpty(delta))
                {
                    yield return delta;
                }

                continue;
            }

            assembled += text;
            yield return text;
        }
    }

    private async Task<IReadOnlyList<string>> DiscoverInternalAsync(string? key, CancellationToken ct)
    {
        try
        {
            using var client = CreateApiClient(key!);
            var pager = await client.Models.ListAsync(new ListModelsConfig(), ct).ConfigureAwait(false);
            var items = new List<string>();

            await foreach (dynamic model in pager.WithCancellation(ct).ConfigureAwait(false))
            {
                var name = model?.Name as string;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    items.Add(name);
                }
            }

            return items
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateSdkException(ex);
        }
    }

    private static Client CreateApiClient(string key)
    {
        return new Client(apiKey: key);
    }

    private static List<Content> BuildContents(List<AiChatMessage> history, string currentMessage)
    {
        var contents = history
            .Select(static item => CreateContent(item.Role, item.Content))
            .ToList();

        contents.Add(CreateContent("user", currentMessage));
        return contents;
    }

    private static Content CreateContent(string? role, string text)
    {
        return new Content
        {
            Role = role?.Trim().ToLowerInvariant() switch
            {
                "assistant" => "model",
                _ => "user"
            },
            Parts =
            [
                new Part
                {
                    Text = text
                }
            ]
        };
    }

    private ProviderRequestException CreateSdkException(Exception ex)
    {
        var message = ex.Message ?? ex.GetType().Name;
        var lower = message.ToLowerInvariant();
        var shouldTryNextKey = lower.Contains("invalid", StringComparison.Ordinal)
            || lower.Contains("incorrect", StringComparison.Ordinal)
            || lower.Contains("quota", StringComparison.Ordinal)
            || lower.Contains("rate limit", StringComparison.Ordinal)
            || lower.Contains("401", StringComparison.Ordinal)
            || lower.Contains("403", StringComparison.Ordinal)
            || lower.Contains("429", StringComparison.Ordinal)
            || lower.Contains("timeout", StringComparison.Ordinal)
            || lower.Contains("temporar", StringComparison.Ordinal)
            || lower.Contains("unavailable", StringComparison.Ordinal);

        return new ProviderRequestException($"{ProviderName}: {message}", shouldTryNextKey, innerException: ex);
    }
}
