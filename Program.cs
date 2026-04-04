
using System.Text;
using AIConsoleApp.Models;
using AIConsoleApp.Providers;
using AIConsoleApp.Services;

namespace AIConsoleApp;

internal static class Program
{
    private static readonly string WorkspaceRoot = Environment.CurrentDirectory;

    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        var configManager = new ConfigManager();
        var config = await configManager.LoadAsync().ConfigureAwait(false);
        await EnsureLanguageConfiguredAsync(config, configManager).ConfigureAwait(false);
        config.Normalize();

        var keyManager = new KeyManager(config);
        var logger = new FileAppLogger(configManager.ConfigDirectory);

        PrintBanner(configManager.ConfigPath, logger.LogDirectory, config);

        while (true)
        {
            var text = ConsoleText.For(config.Language);

            ConsoleUi.WritePrompt(config);

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

    private static async Task<bool> HandleCommandAsync(string input, AppConfig config, KeyManager keyManager, ConfigManager configManager, IAppLogger logger)
    {
        var text = ConsoleText.For(config.Language);
        var tokens = CommandTokenizer.Tokenize(input);
        if (tokens.Count == 0)
        {
            return false;
        }

        switch (tokens[0].ToLowerInvariant())
        {
            case "/help":
                Console.WriteLine(text.HelpText);
                return false;
            case "/exit":
                return true;
            case "/language":
                return await HandleLanguageCommandAsync(tokens, config, configManager).ConfigureAwait(false);
            case "/clear":
                GetCurrentHistory(config).Clear();
                SyncCurrentSession(config);
                await configManager.SaveAsync(config).ConfigureAwait(false);
                WriteInfo(text.HistoryCleared);
                return false;
            case "/history":
                PrintHistory(config, tokens.Count > 1 && int.TryParse(tokens[1], out var count) ? count : 10);
                return false;
            case "/addkey":
                return await HandleAddKeyAsync(tokens, config, keyManager, configManager).ConfigureAwait(false);
            case "/listkeys":
                return HandleListKeys(config, keyManager);
            case "/delkey":
                return await HandleDeleteKeyAsync(tokens, config, keyManager, configManager).ConfigureAwait(false);
            case "/activekey":
                return await HandleActiveKeyAsync(tokens, config, keyManager, configManager).ConfigureAwait(false);
            case "/model":
                return await HandleModelCommandAsync(tokens, config, keyManager, configManager, logger).ConfigureAwait(false);
            case "/ask":
                return await HandleAskAsync(tokens, config, keyManager, configManager, logger).ConfigureAwait(false);
            case "/savechat":
                return await HandleSaveChatAsync(tokens, config, configManager).ConfigureAwait(false);
            case "/savemd":
                return await HandleSaveMarkdownAsync(tokens, config).ConfigureAwait(false);
            case "/loadchat":
                return await HandleLoadChatAsync(tokens, config, configManager).ConfigureAwait(false);
            case "/stream":
                return await HandleStreamCommandAsync(tokens, config, configManager).ConfigureAwait(false);
            case "/timeout":
                return await HandleTimeoutCommandAsync(tokens, config, configManager).ConfigureAwait(false);
            case "/retries":
                return await HandleRetriesCommandAsync(tokens, config, configManager).ConfigureAwait(false);
            case "/ls":
                HandleListFiles(tokens, config);
                return false;
            case "/cd":
                return await HandleChangeDirectoryAsync(tokens, config, configManager).ConfigureAwait(false);
            case "/read":
                HandleReadFile(tokens, config);
                return false;
            case "/mkdir":
                return await HandleMakeDirectoryAsync(tokens, config).ConfigureAwait(false);
            case "/write":
                return await HandleWriteFileAsync(tokens, config).ConfigureAwait(false);
            case "/doctor":
                await HandleDoctorAsync(config, keyManager, configManager, Path.Combine(configManager.ConfigDirectory, "logs")).ConfigureAwait(false);
                return false;
            case "/chat":
                return await HandleChatCommandAsync(tokens, config, configManager).ConfigureAwait(false);
            default:
                throw new InvalidOperationException(text.UnknownCommand(tokens[0]));
        }
    }

    private static async Task<bool> HandleLanguageCommandAsync(IReadOnlyList<string> tokens, AppConfig config, ConfigManager configManager)
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

    private static async Task<bool> HandleAddKeyAsync(IReadOnlyList<string> tokens, AppConfig config, KeyManager keyManager, ConfigManager configManager)
    {
        var text = ConsoleText.For(config.Language);
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
    private static bool HandleListKeys(AppConfig config, KeyManager keyManager)
    {
        var text = ConsoleText.For(config.Language);
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

    private static async Task<bool> HandleDeleteKeyAsync(IReadOnlyList<string> tokens, AppConfig config, KeyManager keyManager, ConfigManager configManager)
    {
        var text = ConsoleText.For(config.Language);
        var args = CommandTokenizer.ParseNamedArguments(tokens.Skip(1));
        if (!args.TryGetValue("provider", out var provider))
        {
            throw new InvalidOperationException(text.DeleteKeyUsage);
        }

        var deleteAll = tokens.Skip(1).Any(token => string.Equals(token, "all", StringComparison.OrdinalIgnoreCase));
        var removed = deleteAll ? keyManager.DeleteAllKeys(provider) : args.TryGetValue("key", out var key) && keyManager.DeleteKey(provider, key);
        if (!removed)
        {
            WriteInfo(text.NothingRemoved);
            return false;
        }

        await configManager.SaveAsync(config).ConfigureAwait(false);
        WriteInfo(deleteAll ? text.AllKeysDeleted(ModelCatalog.NormalizeProvider(provider)) : text.KeyDeleted);
        return false;
    }

    private static async Task<bool> HandleActiveKeyAsync(IReadOnlyList<string> tokens, AppConfig config, KeyManager keyManager, ConfigManager configManager)
    {
        var text = ConsoleText.For(config.Language);
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

    private static async Task<bool> HandleAskAsync(IReadOnlyList<string> tokens, AppConfig config, KeyManager keyManager, ConfigManager configManager, IAppLogger logger)
    {
        var text = ConsoleText.For(config.Language);
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

    private static async Task<bool> HandleSaveChatAsync(IReadOnlyList<string> tokens, AppConfig config, ConfigManager configManager)
    {
        var text = ConsoleText.For(config.Language);
        if (tokens.Count < 2)
        {
            throw new InvalidOperationException(text.SaveChatUsage);
        }

        var transcript = new ChatTranscript
        {
            Provider = config.ActiveProvider,
            Model = config.ActiveModel,
            Messages = GetCurrentHistory(config).Select(static message => message.Clone()).ToList()
        };

        await configManager.SaveTranscriptAsync(tokens[1], transcript).ConfigureAwait(false);
        WriteInfo(text.TranscriptSaved(Path.GetFullPath(tokens[1])));
        return false;
    }

    private static async Task<bool> HandleSaveMarkdownAsync(IReadOnlyList<string> tokens, AppConfig config)
    {
        var text = ConsoleText.For(config.Language);
        if (tokens.Count < 2)
        {
            throw new InvalidOperationException(text.SaveMarkdownUsage);
        }

        var path = Path.GetFullPath(tokens[1]);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine($"# Chat Export: {config.CurrentSessionName}");
        builder.AppendLine();
        builder.AppendLine($"- Provider: `{config.ActiveProvider}`");
        builder.AppendLine($"- Model: `{config.ActiveModel}`");
        builder.AppendLine();

        foreach (var item in GetCurrentHistory(config))
        {
            builder.AppendLine($"## {item.Role} ({item.Timestamp:yyyy-MM-dd HH:mm:ss})");
            builder.AppendLine();
            builder.AppendLine(item.Content);
            builder.AppendLine();
        }

        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8).ConfigureAwait(false);
        WriteInfo(text.TranscriptSaved(path));
        return false;
    }

    private static async Task<bool> HandleLoadChatAsync(IReadOnlyList<string> tokens, AppConfig config, ConfigManager configManager)
    {
        var text = ConsoleText.For(config.Language);
        if (tokens.Count < 2)
        {
            throw new InvalidOperationException(text.LoadChatUsage);
        }

        var transcript = await configManager.LoadTranscriptAsync(tokens[1]).ConfigureAwait(false);
        config.ActiveProvider = ModelCatalog.NormalizeProvider(transcript.Provider);
        config.ActiveModel = transcript.Model;
        var session = EnsureSession(config, config.CurrentSessionName);
        session.Messages = transcript.Messages.Select(static message => message.Clone()).ToList();
        SyncCurrentSession(config);
        await configManager.SaveAsync(config).ConfigureAwait(false);
        WriteInfo(text.TranscriptLoaded(Path.GetFullPath(tokens[1])));
        return false;
    }

    private static async Task<bool> HandleStreamCommandAsync(IReadOnlyList<string> tokens, AppConfig config, ConfigManager configManager)
    {
        if (tokens.Count == 1)
        {
            WriteInfo($"stream={(config.StreamingEnabled ? "on" : "off")}");
            return false;
        }

        config.StreamingEnabled = tokens[1].Equals("on", StringComparison.OrdinalIgnoreCase) || tokens[1].Equals("true", StringComparison.OrdinalIgnoreCase);
        await configManager.SaveAsync(config).ConfigureAwait(false);
        WriteInfo($"stream={(config.StreamingEnabled ? "on" : "off")}");
        return false;
    }

    private static async Task<bool> HandleTimeoutCommandAsync(IReadOnlyList<string> tokens, AppConfig config, ConfigManager configManager)
    {
        if (tokens.Count == 1)
        {
            WriteInfo($"timeout={config.RequestTimeoutSeconds}s");
            return false;
        }

        if (!int.TryParse(tokens[1], out var seconds) || seconds <= 0)
        {
            throw new InvalidOperationException("Usage: /timeout 120");
        }

        config.RequestTimeoutSeconds = seconds;
        await configManager.SaveAsync(config).ConfigureAwait(false);
        WriteInfo($"timeout={config.RequestTimeoutSeconds}s");
        return false;
    }

    private static async Task<bool> HandleRetriesCommandAsync(IReadOnlyList<string> tokens, AppConfig config, ConfigManager configManager)
    {
        if (tokens.Count == 1)
        {
            WriteInfo($"retries={config.MaxRetriesPerKey}");
            return false;
        }

        if (!int.TryParse(tokens[1], out var retries) || retries < 0)
        {
            throw new InvalidOperationException("Usage: /retries 3");
        }

        config.MaxRetriesPerKey = retries;
        await configManager.SaveAsync(config).ConfigureAwait(false);
        WriteInfo($"retries={config.MaxRetriesPerKey}");
        return false;
    }
    private static async Task<string?> TryHandleLocalActionAsync(string input, AppConfig config, KeyManager keyManager, ConfigManager configManager, IAppLogger logger)
    {
        var selection = ResolveSelection(config, null, null);
        var provider = CreateProvider(config, selection.Provider, selection.Model, keyManager, logger);
        var history = GetCurrentHistory(config).Select(static message => message.Clone()).ToList();

        var result = await AiActionPlanner.TryExecuteAsync(
            input,
            WorkspaceRoot,
            GetCurrentDirectoryFullPath(config),
            history,
            provider,
            rawPath => ResolveWorkspacePath(config, rawPath),
            directory => UpdateCurrentWorkingDirectory(config, directory),
            CancellationToken.None).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(result))
        {
            var currentHistory = GetCurrentHistory(config);
            currentHistory.Add(new ChatMessage { Role = "user", Content = input, Timestamp = DateTimeOffset.UtcNow });
            currentHistory.Add(new ChatMessage { Role = "assistant", Content = result, Timestamp = DateTimeOffset.UtcNow });
            config.ActiveProvider = selection.Provider;
            config.ActiveModel = selection.Model;
            SyncCurrentSession(config);
            await configManager.SaveAsync(config).ConfigureAwait(false);
        }

        return result;
    }

    private static async Task<bool> HandleChangeDirectoryAsync(IReadOnlyList<string> tokens, AppConfig config, ConfigManager configManager)
    {
        if (tokens.Count < 2)
        {
            throw new InvalidOperationException("Usage: /cd path/to/folder");
        }

        var fullPath = ResolveWorkspacePath(config, tokens[1]);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(fullPath);
        }

        UpdateCurrentWorkingDirectory(config, fullPath);
        await configManager.SaveAsync(config).ConfigureAwait(false);
        WriteInfo(fullPath);
        return false;
    }

    private static void HandleListFiles(IReadOnlyList<string> tokens, AppConfig config)
    {
        var args = CommandTokenizer.ParseNamedArguments(tokens.Skip(1));
        var target = args.TryGetValue("path", out var namedPath) ? namedPath : tokens.Count > 1 ? tokens[1] : ".";
        var fullPath = ResolveWorkspacePath(config, target);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(fullPath);
        }

        foreach (var item in Directory.GetFileSystemEntries(fullPath).OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(item);
            var kind = Directory.Exists(item) ? "dir " : "file";
            Console.WriteLine($"[{kind}] {name}");
        }
    }

    private static void HandleReadFile(IReadOnlyList<string> tokens, AppConfig config)
    {
        if (tokens.Count < 2)
        {
            throw new InvalidOperationException("Usage: /read path/to/file");
        }

        var path = ResolveWorkspacePath(config, tokens[1]);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(path);
        }

        Console.WriteLine(File.ReadAllText(path));
    }

    private static Task<bool> HandleMakeDirectoryAsync(IReadOnlyList<string> tokens, AppConfig config)
    {
        if (tokens.Count < 2)
        {
            throw new InvalidOperationException("Usage: /mkdir path/to/folder");
        }

        var path = ResolveWorkspacePath(config, tokens[1]);
        Directory.CreateDirectory(path);
        WriteInfo($"Directory created: {path}");
        return Task.FromResult(false);
    }

    private static async Task<bool> HandleWriteFileAsync(IReadOnlyList<string> tokens, AppConfig config)
    {
        if (tokens.Count < 3)
        {
            throw new InvalidOperationException("Usage: /write file.txt \"content\"");
        }

        var path = ResolveWorkspacePath(config, tokens[1]);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = string.Join(' ', tokens.Skip(2));
        await File.WriteAllTextAsync(path, content, Encoding.UTF8).ConfigureAwait(false);
        WriteInfo($"File written: {path}");
        return false;
    }

    private static async Task HandleDoctorAsync(AppConfig config, KeyManager keyManager, ConfigManager configManager, string logDirectory)
    {
        Console.WriteLine("Doctor report");
        Console.WriteLine($"- Workspace: {WorkspaceRoot}");
        Console.WriteLine($"- Config: {configManager.ConfigPath}");
        Console.WriteLine($"- Logs: {logDirectory}");
        Console.WriteLine($"- Language: {AppLanguage.NormalizeOrDefault(config.Language)}");
        Console.WriteLine($"- Active model: {config.ActiveProvider}/{config.ActiveModel}");
        Console.WriteLine($"- Streaming: {(config.StreamingEnabled ? "on" : "off")}");
        Console.WriteLine($"- Timeout: {config.RequestTimeoutSeconds}s");
        Console.WriteLine($"- Retries per key: {config.MaxRetriesPerKey}");
        Console.WriteLine($"- Current session: {config.CurrentSessionName}");
        Console.WriteLine($"- Sessions: {config.Sessions.Count}");
        Console.WriteLine($"- Current session messages: {GetCurrentHistory(config).Count}");

        foreach (var provider in ModelCatalog.GetProviders())
        {
            var state = provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) ? "local" : (keyManager.HasAnyKey(provider) ? "configured" : "missing-key");
            Console.WriteLine($"- Provider {provider}: {state}");
        }

        Console.WriteLine($"- Runtime: {Environment.Version}");
        Console.WriteLine($"- Ollama base: {Environment.GetEnvironmentVariable("AICONSOLE_OLLAMA_BASE_URL") ?? "http://localhost:11434"}");
        await Task.CompletedTask;
    }

    private static async Task<bool> HandleChatCommandAsync(IReadOnlyList<string> tokens, AppConfig config, ConfigManager configManager)
    {
        if (tokens.Count == 1 || tokens[1].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var session in config.Sessions.Values.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                var marker = session.Name.Equals(config.CurrentSessionName, StringComparison.OrdinalIgnoreCase) ? '*' : ' ';
                Console.WriteLine($"{marker} {session.Name} ({session.Messages.Count} messages)");
            }

            return false;
        }

        if (tokens.Count < 3)
        {
            throw new InvalidOperationException("Usage: /chat list | /chat new name | /chat switch name | /chat delete name");
        }

        var action = tokens[1].ToLowerInvariant();
        var sessionName = AppConfig.NormalizeSessionName(tokens[2]);
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            throw new InvalidOperationException("Session name cannot be empty.");
        }

        switch (action)
        {
            case "new":
                if (!config.Sessions.ContainsKey(sessionName))
                {
                    config.Sessions[sessionName] = new ChatSession { Name = sessionName };
                }
                config.CurrentSessionName = sessionName;
                SyncCurrentSession(config);
                await configManager.SaveAsync(config).ConfigureAwait(false);
                WriteInfo($"Switched to session: {sessionName}");
                return false;
            case "switch":
                EnsureSession(config, sessionName);
                config.CurrentSessionName = sessionName;
                SyncCurrentSession(config);
                await configManager.SaveAsync(config).ConfigureAwait(false);
                WriteInfo($"Switched to session: {sessionName}");
                return false;
            case "delete":
                if (sessionName.Equals(config.CurrentSessionName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Cannot delete the active session. Switch first.");
                }
                config.Sessions.Remove(sessionName);
                await configManager.SaveAsync(config).ConfigureAwait(false);
                WriteInfo($"Deleted session: {sessionName}");
                return false;
            default:
                throw new InvalidOperationException("Usage: /chat list | /chat new name | /chat switch name | /chat delete name");
        }
    }
    private static async Task<bool> HandleModelCommandAsync(IReadOnlyList<string> tokens, AppConfig config, KeyManager keyManager, ConfigManager configManager, IAppLogger logger)
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

    private static async Task SendPromptAsync(string prompt, string? providerOverride, string? modelOverride, AppConfig config, KeyManager keyManager, ConfigManager configManager, IAppLogger logger)
    {
        if (string.IsNullOrWhiteSpace(providerOverride) && string.IsNullOrWhiteSpace(modelOverride))
        {
            var localActionResult = await TryHandleLocalActionAsync(prompt, config, keyManager, configManager, logger).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(localActionResult))
            {
                ConsoleUi.WriteAssistantPrefix(config.ActiveProvider, config.ActiveModel);
                Console.Write(localActionResult);
                ConsoleUi.FinishAssistantBlock();
                return;
            }
        }

        var selection = ResolveSelection(config, providerOverride, modelOverride);
        var provider = CreateProvider(config, selection.Provider, selection.Model, keyManager, logger);
        var history = GetCurrentHistory(config).Select(static message => message.Clone()).ToList();

        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler? handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += handler;
        try
        {
            ConsoleUi.WriteAssistantPrefix(selection.Provider, selection.Model);

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

            ConsoleUi.FinishAssistantBlock();
            var currentHistory = GetCurrentHistory(config);
            currentHistory.Add(new ChatMessage { Role = "user", Content = prompt, Timestamp = DateTimeOffset.UtcNow });
            currentHistory.Add(new ChatMessage { Role = "assistant", Content = responseBuilder.ToString(), Timestamp = DateTimeOffset.UtcNow });

            config.ActiveProvider = selection.Provider;
            config.ActiveModel = selection.Model;
            SyncCurrentSession(config);
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
            ? (!string.IsNullOrWhiteSpace(config.ActiveProvider) ? ModelCatalog.NormalizeProvider(config.ActiveProvider) : InferProvider(config, config.ActiveModel))
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

        return (provider, NormalizeKnownModel(config, provider, requestedModel));
    }

    private static async Task PrintModelsAsync(AppConfig config, KeyManager keyManager, ConfigManager configManager, IAppLogger logger)
    {
        var text = ConsoleText.For(config.Language);
        var refreshedProviders = await RefreshDiscoveredModelsAsync(config, keyManager, configManager, logger).ConfigureAwait(false);

        foreach (var provider in GetOrderedProviders(config))
        {
            Console.WriteLine(provider);
            var hasKey = string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase) ? text.NotApplicable : keyManager.HasAnyKey(provider) ? text.Yes : text.No;
            var discoveredModels = config.DiscoveredModels.TryGetValue(provider, out var cachedModels) ? cachedModels : [];
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
    private static async Task<HashSet<string>> RefreshDiscoveredModelsAsync(AppConfig config, KeyManager keyManager, ConfigManager configManager, IAppLogger logger)
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
                var defaultModel = string.Equals(config.ActiveProvider, providerName, StringComparison.OrdinalIgnoreCase) ? config.ActiveModel : ModelCatalog.GetDefaultForProvider(providerName).ModelId;
                var provider = CreateProvider(config, providerName, defaultModel, keyManager, logger);
                var liveModels = await provider.DiscoverModelsAsync(CancellationToken.None).ConfigureAwait(false);
                if (liveModels.Count == 0)
                {
                    continue;
                }

                refreshed.Add(providerName);
                var normalized = liveModels.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToList();
                if (!config.DiscoveredModels.TryGetValue(providerName, out var existing) || !existing.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase))
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
        return ModelCatalog.GetProviders().Concat(config.DiscoveredModels.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase).ToList();
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
        var items = GetCurrentHistory(config).TakeLast(count).ToList();
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
        ConsoleUi.PrintBanner(text, configPath, logDirectory, config);
    }

    private static List<ChatMessage> GetCurrentHistory(AppConfig config)
    {
        var session = EnsureSession(config, config.CurrentSessionName);
        config.ChatHistory = session.Messages;
        return session.Messages;
    }

    private static ChatSession EnsureSession(AppConfig config, string name)
    {
        var normalized = AppConfig.NormalizeSessionName(name);
        if (!config.Sessions.TryGetValue(normalized, out var session))
        {
            session = new ChatSession { Name = normalized };
            config.Sessions[normalized] = session;
        }

        session.Messages ??= new List<ChatMessage>();
        return session;
    }

    private static void SyncCurrentSession(AppConfig config)
    {
        var session = EnsureSession(config, config.CurrentSessionName);
        session.Messages = config.ChatHistory.Select(static message => message.Clone()).ToList();
        config.ChatHistory = session.Messages;
    }

    private static string GetCurrentDirectoryFullPath(AppConfig config)
    {
        var current = string.IsNullOrWhiteSpace(config.CurrentWorkingDirectory) ? "." : config.CurrentWorkingDirectory;
        return ResolveWorkspacePath(config, current);
    }

    private static void UpdateCurrentWorkingDirectory(AppConfig config, string fullPath)
    {
        var relative = Path.GetRelativePath(WorkspaceRoot, fullPath);
        config.CurrentWorkingDirectory = string.IsNullOrWhiteSpace(relative) ? "." : relative;
    }

    private static string ResolveWorkspacePath(AppConfig config, string rawPath)
    {
        var baseDirectory = GetBaseDirectory(config);
        var combined = Path.GetFullPath(Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(baseDirectory, rawPath));
        var workspaceFull = Path.GetFullPath(WorkspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCombined = combined.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!string.Equals(normalizedCombined, workspaceFull, StringComparison.OrdinalIgnoreCase)
            && !normalizedCombined.StartsWith(workspaceFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path must stay inside the current workspace.");
        }

        return combined;
    }

    private static string GetBaseDirectory(AppConfig config)
    {
        var current = string.IsNullOrWhiteSpace(config.CurrentWorkingDirectory) ? "." : config.CurrentWorkingDirectory;
        return Path.GetFullPath(Path.Combine(WorkspaceRoot, current));
    }

    private static void WriteInfo(string message)
    {
        ConsoleUi.WriteInfo(message);
    }

    private static void WriteError(string message)
    {
        ConsoleUi.WriteError(message);
    }
}










