using System.Text;
using AIConsoleApp.Models;
using AIConsoleApp.Providers;
using AIConsoleApp.Services;

namespace AIConsoleApp;

internal static class Program
{
    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        var configManager = new ConfigManager();
        var config = await configManager.LoadAsync().ConfigureAwait(false);
        await EnsureLanguageConfiguredAsync(config, configManager).ConfigureAwait(false);

        var keyManager = new KeyManager(config);
        var logger = new FileAppLogger(configManager.ConfigDirectory);

        PrintBanner(configManager.ConfigPath, logger.LogDirectory, config);

        while (true)
        {
            var text = ConsoleText.For(config.Language);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{config.ActiveProvider}/{config.ActiveModel}> ");
            Console.ResetColor();

            var input = Console.ReadLine();
            if (input is null)
            {
                await configManager.SaveAsync(config).ConfigureAwait(false);
                return;
            }

            input = input.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            try
            {
                if (input.StartsWith('/'))
                {
                    var shouldExit = await HandleCommandAsync(input, config, keyManager, configManager, logger).ConfigureAwait(false);
                    if (shouldExit)
                    {
                        await configManager.SaveAsync(config).ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    await SendPromptAsync(input, null, null, config, keyManager, configManager, logger).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                await logger.WarningAsync("interactive request canceled by user").ConfigureAwait(false);
                WriteInfo(text.RequestCanceled);
            }
            catch (Exception ex)
            {
                await logger.ErrorAsync(ex.ToString()).ConfigureAwait(false);
                WriteError(ex.Message);
            }
        }
    }

    private static async Task EnsureLanguageConfiguredAsync(AppConfig config, ConfigManager configManager)
    {
        var normalized = AppLanguage.Normalize(config.Language);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            config.Language = normalized;
            return;
        }

        while (true)
        {
            Console.WriteLine("Choose language / Выберите язык:");
            Console.WriteLine("  1. English");
            Console.WriteLine("  2. Русский");
            Console.Write("> ");

            var input = Console.ReadLine()?.Trim();
            var selected = input switch
            {
                "1" => AppLanguage.English,
                "2" => AppLanguage.Russian,
                _ => AppLanguage.Normalize(input)
            };

            if (!string.IsNullOrWhiteSpace(selected))
            {
                config.Language = selected;
                await configManager.SaveAsync(config).ConfigureAwait(false);
                var text = ConsoleText.For(config.Language);
                WriteInfo(text.LanguageUpdated(text.DescribeLanguage(config.Language)));
                Console.WriteLine();
                return;
            }

            WriteError("Please enter 1, 2, english, russian, en, or ru. / Введите 1, 2, english, russian, en или ru.");
        }
    }

    private static async Task<bool> HandleCommandAsync(
        string input,
        AppConfig config,
        KeyManager keyManager,
        ConfigManager configManager,
        IAppLogger logger)
    {
        var text = ConsoleText.For(config.Language);
        var tokens = CommandTokenizer.Tokenize(input);
        if (tokens.Count == 0)
        {
            return false;
        }

        var command = tokens[0].ToLowerInvariant();
        switch (command)
        {
            case "/help":
                Console.WriteLine(text.HelpText);
                return false;

            case "/exit":
                return true;

            case "/language":
                return await HandleLanguageCommandAsync(tokens, config, configManager).ConfigureAwait(false);

            case "/clear":
                config.ChatHistory.Clear();
                await configManager.SaveAsync(config).ConfigureAwait(false);
                WriteInfo(text.HistoryCleared);
                return false;

            case "/history":
                PrintHistory(config, tokens.Count > 1 && int.TryParse(tokens[1], out var count) ? count : 10);
                return false;

            case "/addkey":
            {
                var args = CommandTokenizer.ParseNamedArguments(tokens.Skip(1));
                if (!args.TryGetValue("provider", out var provider) || !args.TryGetValue("key", out var key))
                {
                    throw new InvalidOperationException(text.AddKeyUsage);
                }

                if (!keyManager.AddKey(provider, key))
                {
                    WriteInfo(text.DuplicateOrInvalidKey);
                    return false;
                }

                await configManager.SaveAsync(config).ConfigureAwait(false);
                WriteInfo(text.KeyAdded(ModelCatalog.NormalizeProvider(provider)));
                return false;
            }

            case "/listkeys":
            {
                var items = keyManager.ListKeys();
                if (items.Count == 0)
                {
                    WriteInfo(text.NoSavedKeys);
                    return false;
                }

                foreach (var item in items)
                {
                    Console.WriteLine($"- {item.Provider,-10} {item.MaskedKey,-20} {text.ActiveState(item.IsActive)}");
                }

                return false;
            }

            case "/delkey":
            {
                var args = CommandTokenizer.ParseNamedArguments(tokens.Skip(1));
                if (!args.TryGetValue("provider", out var provider))
                {
                    throw new InvalidOperationException(text.DeleteKeyUsage);
                }

                var deleteAll = tokens.Skip(1).Any(token => string.Equals(token, "all", StringComparison.OrdinalIgnoreCase));
                var removed = deleteAll
                    ? keyManager.DeleteAllKeys(provider)
                    : args.TryGetValue("key", out var key) && keyManager.DeleteKey(provider, key);

                if (!removed)
                {
                    WriteInfo(text.NothingRemoved);
                    return false;
                }

                await configManager.SaveAsync(config).ConfigureAwait(false);
                WriteInfo(deleteAll
                    ? text.AllKeysDeleted(ModelCatalog.NormalizeProvider(provider))
                    : text.KeyDeleted);
                return false;
            }

            case "/activekey":
            {
                var args = CommandTokenizer.ParseNamedArguments(tokens.Skip(1));
                if (!args.TryGetValue("provider", out var provider))
                {
                    throw new InvalidOperationException(text.ActiveKeyUsage);
                }

                args.TryGetValue("key", out var key);
                if (!keyManager.SetActiveKey(provider, key))
                {
                    WriteInfo(text.ActiveKeyNotFound);
                    return false;
                }

                await configManager.SaveAsync(config).ConfigureAwait(false);
                WriteInfo(text.ActiveKeyUpdated(ModelCatalog.NormalizeProvider(provider)));
                return false;
            }

            case "/model":
                return await HandleModelCommandAsync(tokens, config, keyManager, configManager, logger).ConfigureAwait(false);

            case "/ask":
            {
                var args = CommandTokenizer.ParseNamedArguments(tokens.Skip(1));
                var questionParts = tokens.Skip(1).Where(static token => !token.Contains('=')).ToList();
                if (questionParts.Count == 0)
                {
                    throw new InvalidOperationException(text.AskUsage);
                }

                var question = string.Join(' ', questionParts);
                args.TryGetValue("provider", out var providerOverride);
                args.TryGetValue("model", out var modelOverride);
                await SendPromptAsync(question, providerOverride, modelOverride, config, keyManager, configManager, logger).ConfigureAwait(false);
                return false;
            }

            case "/savechat":
            {
                if (tokens.Count < 2)
                {
                    throw new InvalidOperationException(text.SaveChatUsage);
                }

                var transcript = new ChatTranscript
                {
                    Provider = config.ActiveProvider,
                    Model = config.ActiveModel,
                    Messages = config.ChatHistory.Select(static message => message.Clone()).ToList()
                };

                await configManager.SaveTranscriptAsync(tokens[1], transcript).ConfigureAwait(false);
                WriteInfo(text.TranscriptSaved(Path.GetFullPath(tokens[1])));
                return false;
            }

            case "/loadchat":
            {
                if (tokens.Count < 2)
                {
                    throw new InvalidOperationException(text.LoadChatUsage);
                }

                var transcript = await configManager.LoadTranscriptAsync(tokens[1]).ConfigureAwait(false);
                config.ChatHistory = transcript.Messages.Select(static message => message.Clone()).ToList();
                config.ActiveProvider = ModelCatalog.NormalizeProvider(transcript.Provider);
                config.ActiveModel = transcript.Model;
                await configManager.SaveAsync(config).ConfigureAwait(false);
                WriteInfo(text.TranscriptLoaded(Path.GetFullPath(tokens[1])));
                return false;
            }

            default:
                throw new InvalidOperationException(text.UnknownCommand(command));
        }
    }

    private static async Task<bool> HandleLanguageCommandAsync(
        IReadOnlyList<string> tokens,
        AppConfig config,
        ConfigManager configManager)
    {
        var text = ConsoleText.For(config.Language);
        if (tokens.Count == 1)
        {
            WriteInfo(text.CurrentLanguage(text.DescribeLanguage(config.Language)));
            return false;
        }

        string? requestedLanguage = null;
        if (tokens.Count >= 3 && string.Equals(tokens[1], "set", StringComparison.OrdinalIgnoreCase))
        {
            requestedLanguage = tokens[2];
        }
        else if (tokens.Count >= 2)
        {
            requestedLanguage = tokens[1];
        }

        var normalized = AppLanguage.Normalize(requestedLanguage);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(text.LanguageUsage);
        }

        config.Language = normalized;
        await configManager.SaveAsync(config).ConfigureAwait(false);

        var updatedText = ConsoleText.For(config.Language);
        WriteInfo(updatedText.LanguageUpdated(updatedText.DescribeLanguage(config.Language)));
        return false;
    }

    private static async Task<bool> HandleModelCommandAsync(
        IReadOnlyList<string> tokens,
        AppConfig config,
        KeyManager keyManager,
        ConfigManager configManager,
        IAppLogger logger)
    {
        var text = ConsoleText.For(config.Language);
        if (tokens.Count == 1)
        {
            WriteInfo(text.CurrentModel(config.ActiveProvider, config.ActiveModel));
            return false;
        }

        if (string.Equals(tokens[1], "list", StringComparison.OrdinalIgnoreCase))
        {
            await PrintModelsAsync(config, keyManager, configManager, logger).ConfigureAwait(false);
            return false;
        }

        if (string.Equals(tokens[1], "set", StringComparison.OrdinalIgnoreCase))
        {
            var args = CommandTokenizer.ParseNamedArguments(tokens.Skip(2));
            args.TryGetValue("provider", out var providerArg);
            args.TryGetValue("model", out var modelArg);
            if (string.IsNullOrWhiteSpace(modelArg) && string.IsNullOrWhiteSpace(providerArg))
            {
                throw new InvalidOperationException(text.ModelSetUsage);
            }

            var selection = ResolveSelection(config, providerArg, modelArg);
            config.ActiveProvider = selection.Provider;
            config.ActiveModel = selection.Model;
            await configManager.SaveAsync(config).ConfigureAwait(false);
            WriteInfo(text.ActiveModelUpdated(selection.Provider, selection.Model));
            return false;
        }

        throw new InvalidOperationException(text.ModelCommandUsage);
    }

    private static async Task SendPromptAsync(
        string prompt,
        string? providerOverride,
        string? modelOverride,
        AppConfig config,
        KeyManager keyManager,
        ConfigManager configManager,
        IAppLogger logger)
    {
        var selection = ResolveSelection(config, providerOverride, modelOverride);
        var provider = CreateProvider(config, selection.Provider, selection.Model, keyManager, logger);
        var history = config.ChatHistory.Select(static message => message.Clone()).ToList();

        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler? handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += handler;
        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"[{selection.Provider}/{selection.Model}] ");
            Console.ResetColor();

            var responseBuilder = new StringBuilder();
            if (config.StreamingEnabled)
            {
                await foreach (var chunk in provider.StreamMessageAsync(prompt, history, cts.Token).WithCancellation(cts.Token).ConfigureAwait(false))
                {
                    Console.Write(chunk);
                    responseBuilder.Append(chunk);
                }
            }
            else
            {
                var response = await provider.SendMessageAsync(prompt, history, cts.Token).ConfigureAwait(false);
                Console.Write(response);
                responseBuilder.Append(response);
            }

            Console.WriteLine();
            var assistantMessage = responseBuilder.ToString();

            config.ActiveProvider = selection.Provider;
            config.ActiveModel = selection.Model;
            config.ChatHistory.Add(new ChatMessage
            {
                Role = "user",
                Content = prompt,
                Timestamp = DateTimeOffset.UtcNow
            });
            config.ChatHistory.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantMessage,
                Timestamp = DateTimeOffset.UtcNow
            });

            await configManager.SaveAsync(config).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private static (string Provider, string Model) ResolveSelection(AppConfig config, string? providerOverride, string? modelOverride)
    {
        config.Normalize();

        var provider = string.IsNullOrWhiteSpace(providerOverride)
            ? (!string.IsNullOrWhiteSpace(config.ActiveProvider)
                ? ModelCatalog.NormalizeProvider(config.ActiveProvider)
                : InferProvider(config, config.ActiveModel))
            : ModelCatalog.NormalizeProvider(providerOverride);

        if (string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(modelOverride))
        {
            provider = InferProvider(config, modelOverride);
        }

        provider = string.IsNullOrWhiteSpace(provider) ? "openai" : provider;
        var providerChanged = !string.IsNullOrWhiteSpace(providerOverride)
            && !string.Equals(provider, ModelCatalog.NormalizeProvider(config.ActiveProvider), StringComparison.OrdinalIgnoreCase);
        var requestedModel = string.IsNullOrWhiteSpace(modelOverride)
            ? (providerChanged ? ModelCatalog.GetDefaultForProvider(provider).ModelId : config.ActiveModel)
            : modelOverride.Trim();

        if (string.IsNullOrWhiteSpace(requestedModel))
        {
            requestedModel = ModelCatalog.GetDefaultForProvider(provider).ModelId;
        }

        var normalizedModel = NormalizeKnownModel(config, provider, requestedModel);
        return (provider, normalizedModel);
    }

    private static async Task PrintModelsAsync(
        AppConfig config,
        KeyManager keyManager,
        ConfigManager configManager,
        IAppLogger logger)
    {
        var text = ConsoleText.For(config.Language);
        var refreshedProviders = await RefreshDiscoveredModelsAsync(config, keyManager, configManager, logger).ConfigureAwait(false);

        foreach (var provider in GetOrderedProviders(config))
        {
            Console.WriteLine(provider);
            var hasKey = string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase)
                ? text.NotApplicable
                : keyManager.HasAnyKey(provider) ? text.Yes : text.No;
            var discoveredModels = config.DiscoveredModels.TryGetValue(provider, out var cachedModels)
                ? cachedModels
                : [];
            var liveTag = refreshedProviders.Contains(provider) ? text.LiveTag : discoveredModels.Count > 0 ? text.CachedTag : string.Empty;
            var catalogModels = ModelCatalog.GetModelsForProvider(provider);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var model in catalogModels)
            {
                seen.Add(model.ModelId);
                var isActive = string.Equals(config.ActiveProvider, model.Provider, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(config.ActiveModel, model.ModelId, StringComparison.OrdinalIgnoreCase);
                var extraTags = new List<string>();
                if (discoveredModels.Contains(model.ModelId, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(liveTag))
                {
                    extraTags.Add(liveTag);
                }

                if (!string.IsNullOrWhiteSpace(model.Notes))
                {
                    extraTags.Add(model.Notes);
                }

                var suffix = extraTags.Count == 0 ? string.Empty : $" [{string.Join(", ", extraTags)}]";
                Console.WriteLine($"  {(isActive ? '*' : ' ')} {model.ModelId,-32} {text.KeyState(hasKey)}{suffix}");
            }

            foreach (var discoveredModel in discoveredModels.Where(model => seen.Add(model)).OrderBy(static model => model, StringComparer.OrdinalIgnoreCase))
            {
                var isActive = string.Equals(config.ActiveProvider, provider, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(config.ActiveModel, discoveredModel, StringComparison.OrdinalIgnoreCase);
                var suffix = string.IsNullOrWhiteSpace(liveTag) ? string.Empty : $" [{liveTag}]";
                Console.WriteLine($"  {(isActive ? '*' : ' ')} {discoveredModel,-32} {text.KeyState(hasKey)}{suffix}");
            }

            if (catalogModels.Count == 0 && discoveredModels.Count == 0)
            {
                Console.WriteLine(text.NoKnownModels(hasKey));
            }
        }
    }

    private static async Task<HashSet<string>> RefreshDiscoveredModelsAsync(
        AppConfig config,
        KeyManager keyManager,
        ConfigManager configManager,
        IAppLogger logger)
    {
        var refreshed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var providerName in GetOrderedProviders(config))
        {
            var canDiscover = string.Equals(providerName, "ollama", StringComparison.OrdinalIgnoreCase) || keyManager.HasAnyKey(providerName);
            if (!canDiscover)
            {
                continue;
            }

            try
            {
                var defaultModel = string.Equals(config.ActiveProvider, providerName, StringComparison.OrdinalIgnoreCase)
                    ? config.ActiveModel
                    : ModelCatalog.GetDefaultForProvider(providerName).ModelId;
                var provider = CreateProvider(config, providerName, defaultModel, keyManager, logger);
                var liveModels = await provider.DiscoverModelsAsync(CancellationToken.None).ConfigureAwait(false);
                if (liveModels.Count == 0)
                {
                    continue;
                }

                refreshed.Add(providerName);
                var normalized = liveModels
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!config.DiscoveredModels.TryGetValue(providerName, out var existing)
                    || !existing.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    config.DiscoveredModels[providerName] = normalized;
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                await logger.WarningAsync($"provider={providerName} mode=models status=failed error={ex.Message}").ConfigureAwait(false);
            }
        }

        if (changed)
        {
            await configManager.SaveAsync(config).ConfigureAwait(false);
        }

        return refreshed;
    }

    private static AiProvider CreateProvider(AppConfig config, string provider, string model, KeyManager keyManager, IAppLogger logger)
    {
        return ProviderFactory.Create(provider, model, keyManager, ProviderFactory.CreateRuntimeOptions(config), logger);
    }

    private static IReadOnlyList<string> GetOrderedProviders(AppConfig config)
    {
        return ModelCatalog.GetProviders()
            .Concat(config.DiscoveredModels.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string InferProvider(AppConfig config, string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return "openai";
        }

        var fromCatalog = ModelCatalog.GetAll().FirstOrDefault(candidate => candidate.Matches(model));
        if (fromCatalog is not null)
        {
            return fromCatalog.Provider;
        }

        foreach (var pair in config.DiscoveredModels)
        {
            if (pair.Value.Any(discovered => string.Equals(discovered, model, StringComparison.OrdinalIgnoreCase)))
            {
                return pair.Key;
            }
        }

        return ModelCatalog.InferProviderFromModel(model);
    }

    private static string NormalizeKnownModel(AppConfig config, string provider, string requestedModel)
    {
        provider = ModelCatalog.NormalizeProvider(provider);

        if (config.DiscoveredModels.TryGetValue(provider, out var discoveredModels))
        {
            var discoveredMatch = discoveredModels.FirstOrDefault(model => string.Equals(model, requestedModel, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(discoveredMatch))
            {
                return discoveredMatch;
            }
        }

        var known = ModelCatalog.GetModelsForProvider(provider).FirstOrDefault(candidate => candidate.Matches(requestedModel));
        return known?.ModelId ?? requestedModel.Trim();
    }

    private static void PrintHistory(AppConfig config, int count)
    {
        var text = ConsoleText.For(config.Language);
        count = count <= 0 ? 10 : count;
        var items = config.ChatHistory.TakeLast(count).ToList();
        if (items.Count == 0)
        {
            WriteInfo(text.HistoryEmpty);
            return;
        }

        foreach (var item in items)
        {
            Console.WriteLine($"[{item.Timestamp:yyyy-MM-dd HH:mm:ss}] {item.Role}: {item.Content}");
        }
    }

    private static void PrintBanner(string configPath, string logDirectory, AppConfig config)
    {
        var text = ConsoleText.For(config.Language);
        Console.WriteLine(text.BannerTitle);
        Console.WriteLine($"{text.ConfigLabel}: {configPath}");
        Console.WriteLine($"{text.LogsLabel}: {logDirectory}");
        Console.WriteLine($"{text.ActiveLabel}: {config.ActiveProvider}/{config.ActiveModel}");
        Console.WriteLine(text.BannerFooter);
        Console.WriteLine();
    }

    private static void WriteInfo(string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }

    private static void WriteError(string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }
}
