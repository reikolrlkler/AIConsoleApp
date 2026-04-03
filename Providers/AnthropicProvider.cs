using AIConsoleApp.Services;
using Anthropic;
using Anthropic.Models.Models;
using Microsoft.Extensions.AI;
using AiChatMessage = AIConsoleApp.Models.ChatMessage;
using MeaChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AIConsoleApp.Providers;

public sealed class AnthropicProvider : AiProvider
{
    public AnthropicProvider(string model, KeyManager keyManager, HttpClient httpClient, ProviderRuntimeOptions runtimeOptions, IAppLogger logger)
        : base("anthropic", model, keyManager, httpClient, runtimeOptions, logger)
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
            using var client = CreateChatClient(key!);
            var response = await client.GetResponseAsync(BuildMessages(history, message), null, ct).ConfigureAwait(false);
            return response.Text ?? string.Empty;
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
        IChatClient client;

        try
        {
            client = CreateChatClient(key!);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateSdkException(ex);
        }

        using (client)
        {
            await foreach (var update in client.GetStreamingResponseAsync(BuildMessages(history, message), null, ct).WithCancellation(ct).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    yield return update.Text;
                }
            }
        }
    }

    private async Task<IReadOnlyList<string>> DiscoverInternalAsync(string? key, CancellationToken ct)
    {
        try
        {
            using var client = CreateApiClient(key!);
            dynamic page = await client.Models.List(new ModelListParams(), ct).ConfigureAwait(false);
            var items = new List<string>();

            while (page is not null)
            {
                foreach (var model in page.Items)
                {
                    var id = model?.ID as string;
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        items.Add(id);
                    }
                }

                if (!(bool)page.HasNext())
                {
                    break;
                }

                page = await page.Next(ct).ConfigureAwait(false);
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

    private IChatClient CreateChatClient(string key)
    {
        var client = CreateApiClient(key);
        return client.AsIChatClient(Model, 2048);
    }

    private AnthropicClient CreateApiClient(string key)
    {
        return new AnthropicClient
        {
            ApiKey = key,
            MaxRetries = 0,
            Timeout = RuntimeOptions.RequestTimeout
        };
    }

    private static List<MeaChatMessage> BuildMessages(List<AiChatMessage> history, string currentMessage)
    {
        var messages = history
            .Select(static item => new MeaChatMessage(MapRole(item.Role), item.Content))
            .ToList();

        messages.Add(new MeaChatMessage(ChatRole.User, currentMessage));
        return messages;
    }

    private static ChatRole MapRole(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "assistant" => ChatRole.Assistant,
            "system" => ChatRole.System,
            _ => ChatRole.User
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
