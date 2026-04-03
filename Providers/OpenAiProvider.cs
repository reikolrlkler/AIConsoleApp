using System.ClientModel;
using System.Text;
using AIConsoleApp.Services;
using OpenAI.Chat;
using OpenAI.Models;
using AiChatMessage = AIConsoleApp.Models.ChatMessage;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

namespace AIConsoleApp.Providers;

public sealed class OpenAiProvider : AiProvider
{
    public OpenAiProvider(string model, KeyManager keyManager, HttpClient httpClient, ProviderRuntimeOptions runtimeOptions, IAppLogger logger)
        : base("openai", model, keyManager, httpClient, runtimeOptions, logger)
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
            var client = new ChatClient(Model, new ApiKeyCredential(key!));
            var completion = await client.CompleteChatAsync(BuildMessages(history, message), new ChatCompletionOptions(), ct).ConfigureAwait(false);
            return JoinContentParts(completion.Value.Content);
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
        AsyncCollectionResult<StreamingChatCompletionUpdate> stream;

        try
        {
            var client = new ChatClient(Model, new ApiKeyCredential(key!));
            stream = client.CompleteChatStreamingAsync(BuildMessages(history, message), new ChatCompletionOptions(), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateSdkException(ex);
        }

        await foreach (var update in stream.WithCancellation(ct).ConfigureAwait(false))
        {
            var chunk = JoinContentParts(update.ContentUpdate);
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }
    }

    private async Task<IReadOnlyList<string>> DiscoverInternalAsync(string? key, CancellationToken ct)
    {
        try
        {
            var client = new OpenAIModelClient(new ApiKeyCredential(key!));
            dynamic result = await client.GetModelsAsync(ct).ConfigureAwait(false);
            var items = new List<string>();

            foreach (var model in result.Value)
            {
                var id = model?.Id as string;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    items.Add(id);
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

    private static List<OpenAIChatMessage> BuildMessages(List<AiChatMessage> history, string currentMessage)
    {
        var messages = history
            .Select(static item => CreateMessage(item.Role, item.Content))
            .ToList();

        messages.Add(OpenAIChatMessage.CreateUserMessage(currentMessage));
        return messages;
    }

    private static OpenAIChatMessage CreateMessage(string? role, string content)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "assistant" => OpenAIChatMessage.CreateAssistantMessage(content),
            "system" => OpenAIChatMessage.CreateSystemMessage(content),
            _ => OpenAIChatMessage.CreateUserMessage(content)
        };
    }

    private static string JoinContentParts(dynamic content)
    {
        if (content is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var part in content)
        {
            var text = part?.Text as string;
            if (!string.IsNullOrEmpty(text))
            {
                builder.Append(text);
            }
        }

        return builder.ToString();
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
