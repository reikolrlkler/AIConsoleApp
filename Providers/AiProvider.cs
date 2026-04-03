using System.Net;
using System.Runtime.CompilerServices;
using AIConsoleApp.Models;
using AIConsoleApp.Services;

namespace AIConsoleApp.Providers;

public abstract class AiProvider
{
    protected AiProvider(
        string providerName,
        string model,
        KeyManager keyManager,
        HttpClient httpClient,
        ProviderRuntimeOptions runtimeOptions,
        IAppLogger logger,
        bool requiresApiKey = true)
    {
        ProviderName = providerName;
        Model = model;
        KeyManager = keyManager;
        HttpClient = httpClient;
        RuntimeOptions = runtimeOptions;
        Logger = logger;
        RequiresApiKey = requiresApiKey;
    }

    public string ProviderName { get; }

    public string Model { get; }

    protected KeyManager KeyManager { get; }

    protected HttpClient HttpClient { get; }

    protected ProviderRuntimeOptions RuntimeOptions { get; }

    protected IAppLogger Logger { get; }

    protected bool RequiresApiKey { get; }

    public abstract Task<string> SendMessageAsync(string message, List<ChatMessage> history, CancellationToken ct);

    public abstract IAsyncEnumerable<string> StreamMessageAsync(string message, List<ChatMessage> history, CancellationToken ct);

    public virtual Task<IReadOnlyList<string>> DiscoverModelsAsync(CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    protected async Task<T> WithKeyFallbackAsync<T>(
        Func<string?, CancellationToken, Task<T>> requestFactory,
        CancellationToken ct)
    {
        var text = ConsoleText.For(RuntimeOptions.Language);
        var candidateKeys = GetCandidateKeys();
        var failures = new List<string>();

        for (var keyIndex = 0; keyIndex < candidateKeys.Count; keyIndex++)
        {
            var key = candidateKeys[keyIndex];
            var movedToNextKey = false;

            for (var attempt = 1; attempt <= RuntimeOptions.MaxRetriesPerKey + 1; attempt++)
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(RuntimeOptions.RequestTimeout);

                try
                {
                    await Logger.InfoAsync($"provider={ProviderName} mode=send model={Model} keyIndex={keyIndex + 1} attempt={attempt}", ct).ConfigureAwait(false);
                    var result = await requestFactory(key, attemptCts.Token).ConfigureAwait(false);
                    if (key is not null)
                    {
                        KeyManager.MarkKeySuccessful(ProviderName, key);
                    }

                    await Logger.InfoAsync($"provider={ProviderName} mode=send model={Model} keyIndex={keyIndex + 1} attempt={attempt} status=success", ct).ConfigureAwait(false);
                    return result;
                }
                catch (ProviderRequestException ex) when (ex.ShouldTryNextKey)
                {
                    await Logger.WarningAsync($"provider={ProviderName} mode=send model={Model} keyIndex={keyIndex + 1} attempt={attempt} status=retryable error={ex.Message}", ct).ConfigureAwait(false);
                    if (attempt <= RuntimeOptions.MaxRetriesPerKey)
                    {
                        await DelayBeforeRetryAsync(attempt, ct).ConfigureAwait(false);
                        continue;
                    }

                    failures.Add(ex.Message);
                    movedToNextKey = true;
                    break;
                }
                catch (HttpRequestException ex)
                {
                    await Logger.WarningAsync($"provider={ProviderName} mode=send model={Model} keyIndex={keyIndex + 1} attempt={attempt} status=http-error error={ex.Message}", ct).ConfigureAwait(false);
                    if (attempt <= RuntimeOptions.MaxRetriesPerKey)
                    {
                        await DelayBeforeRetryAsync(attempt, ct).ConfigureAwait(false);
                        continue;
                    }

                    failures.Add(text.NetworkOrTlsError(ProviderName, ex.Message));
                    movedToNextKey = true;
                    break;
                }
                catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    var timeoutMessage = text.TimeoutMessage(ProviderName, RuntimeOptions.RequestTimeout.TotalSeconds, ex.Message);
                    await Logger.WarningAsync($"provider={ProviderName} mode=send model={Model} keyIndex={keyIndex + 1} attempt={attempt} status=timeout error={timeoutMessage}", ct).ConfigureAwait(false);
                    if (attempt <= RuntimeOptions.MaxRetriesPerKey)
                    {
                        await DelayBeforeRetryAsync(attempt, ct).ConfigureAwait(false);
                        continue;
                    }

                    failures.Add(timeoutMessage);
                    movedToNextKey = true;
                    break;
                }
            }

            if (!movedToNextKey)
            {
                break;
            }
        }

        throw new InvalidOperationException(BuildAllKeysFailedMessage(failures));
    }

    protected async IAsyncEnumerable<string> WithKeyFallbackStreamAsync(
        Func<string?, CancellationToken, IAsyncEnumerable<string>> requestFactory,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var text = ConsoleText.For(RuntimeOptions.Language);
        var candidateKeys = GetCandidateKeys();
        var failures = new List<string>();

        for (var keyIndex = 0; keyIndex < candidateKeys.Count; keyIndex++)
        {
            var key = candidateKeys[keyIndex];

            for (var attempt = 1; attempt <= RuntimeOptions.MaxRetriesPerKey + 1; attempt++)
            {
                var emittedAnyChunk = false;
                var retryAttempt = false;
                var moveToNextKey = false;
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(RuntimeOptions.RequestTimeout);
                await using var enumerator = requestFactory(key, attemptCts.Token).GetAsyncEnumerator(attemptCts.Token);

                await Logger.InfoAsync($"provider={ProviderName} mode=stream model={Model} keyIndex={keyIndex + 1} attempt={attempt}", ct).ConfigureAwait(false);

                while (true)
                {
                    string? chunk = null;
                    var completed = false;

                    try
                    {
                        if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                        {
                            completed = true;
                        }
                        else
                        {
                            emittedAnyChunk = true;
                            chunk = enumerator.Current;
                        }
                    }
                    catch (ProviderRequestException ex) when (ex.ShouldTryNextKey && !emittedAnyChunk)
                    {
                        await Logger.WarningAsync($"provider={ProviderName} mode=stream model={Model} keyIndex={keyIndex + 1} attempt={attempt} status=retryable error={ex.Message}", ct).ConfigureAwait(false);
                        if (attempt <= RuntimeOptions.MaxRetriesPerKey)
                        {
                            retryAttempt = true;
                        }
                        else
                        {
                            failures.Add(ex.Message);
                            moveToNextKey = true;
                        }

                        break;
                    }
                    catch (HttpRequestException ex) when (!emittedAnyChunk)
                    {
                        await Logger.WarningAsync($"provider={ProviderName} mode=stream model={Model} keyIndex={keyIndex + 1} attempt={attempt} status=http-error error={ex.Message}", ct).ConfigureAwait(false);
                        if (attempt <= RuntimeOptions.MaxRetriesPerKey)
                        {
                            retryAttempt = true;
                        }
                        else
                        {
                            failures.Add(text.NetworkOrTlsError(ProviderName, ex.Message));
                            moveToNextKey = true;
                        }

                        break;
                    }
                    catch (TaskCanceledException ex) when (!ct.IsCancellationRequested && !emittedAnyChunk)
                    {
                        var timeoutMessage = text.TimeoutMessage(ProviderName, RuntimeOptions.RequestTimeout.TotalSeconds, ex.Message);
                        await Logger.WarningAsync($"provider={ProviderName} mode=stream model={Model} keyIndex={keyIndex + 1} attempt={attempt} status=timeout error={timeoutMessage}", ct).ConfigureAwait(false);
                        if (attempt <= RuntimeOptions.MaxRetriesPerKey)
                        {
                            retryAttempt = true;
                        }
                        else
                        {
                            failures.Add(timeoutMessage);
                            moveToNextKey = true;
                        }

                        break;
                    }

                    if (completed)
                    {
                        if (key is not null)
                        {
                            KeyManager.MarkKeySuccessful(ProviderName, key);
                        }

                        await Logger.InfoAsync($"provider={ProviderName} mode=stream model={Model} keyIndex={keyIndex + 1} attempt={attempt} status=success", ct).ConfigureAwait(false);
                        yield break;
                    }

                    if (!string.IsNullOrEmpty(chunk))
                    {
                        yield return chunk;
                    }
                }

                if (retryAttempt)
                {
                    await DelayBeforeRetryAsync(attempt, ct).ConfigureAwait(false);
                    continue;
                }

                if (moveToNextKey)
                {
                    break;
                }
            }
        }

        throw new InvalidOperationException(BuildAllKeysFailedMessage(failures));
    }

    protected ProviderRequestException CreateApiException(HttpStatusCode statusCode, string? body)
    {
        var payload = string.IsNullOrWhiteSpace(body) ? string.Empty : body.Trim();
        if (payload.Length > 320)
        {
            payload = $"{payload[..320]}...";
        }

        var retry = statusCode is HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.RequestTimeout
            || (int)statusCode == 429
            || (int)statusCode >= 500;

        var lowerPayload = payload.ToLowerInvariant();
        if (lowerPayload.Contains("invalid api key", StringComparison.Ordinal)
            || lowerPayload.Contains("incorrect api key", StringComparison.Ordinal)
            || lowerPayload.Contains("quota", StringComparison.Ordinal)
            || lowerPayload.Contains("rate limit", StringComparison.Ordinal)
            || lowerPayload.Contains("expired", StringComparison.Ordinal))
        {
            retry = true;
        }

        return new ProviderRequestException(
            $"{ProviderName}: HTTP {(int)statusCode}. {payload}".Trim(),
            retry,
            statusCode);
    }

    protected string MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "n/a";
        }

        return key.Length <= 10 ? key : $"{key[..6]}...{key[^4..]}";
    }

    private IReadOnlyList<string?> GetCandidateKeys()
    {
        var text = ConsoleText.For(RuntimeOptions.Language);
        if (!RequiresApiKey)
        {
            return new string?[] { null };
        }

        var keys = KeyManager.GetOrderedKeys(ProviderName).Select(static key => (string?)key).ToList();
        if (keys.Count == 0)
        {
            throw new InvalidOperationException(text.NoProviderKeysMessage(ProviderName));
        }

        return keys;
    }

    private string BuildAllKeysFailedMessage(List<string> failures)
    {
        var text = ConsoleText.For(RuntimeOptions.Language);
        var suffix = failures.Count == 0 ? string.Empty : text.RecentErrors(failures.Take(3));
        return text.AllKeysFailedMessage(ProviderName, suffix);
    }

    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken ct)
    {
        var delay = TimeSpan.FromMilliseconds(Math.Min(250 * attempt * attempt, 2000));
        return Task.Delay(delay, ct);
    }
}
