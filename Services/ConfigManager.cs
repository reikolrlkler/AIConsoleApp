using System.Text.Json;
using System.Text.Json.Serialization;
using AIConsoleApp.Models;

namespace AIConsoleApp.Services;

public sealed class ConfigManager
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public ConfigManager()
    {
        ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AIConsole");

        ConfigPath = Path.Combine(ConfigDirectory, "config.json");
    }

    public string ConfigDirectory { get; }

    public string ConfigPath { get; }

    public async Task<AppConfig> LoadAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(ConfigDirectory);

        if (!File.Exists(ConfigPath))
        {
            var freshConfig = new AppConfig();
            freshConfig.Normalize();
            return freshConfig;
        }

        try
        {
            var fileInfo = new FileInfo(ConfigPath);
            if (fileInfo.Length == 0)
            {
                return await RecoverBrokenConfigAsync("empty").ConfigureAwait(false);
            }

            await using var stream = File.OpenRead(ConfigPath);
            var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, _jsonOptions, ct).ConfigureAwait(false)
                ?? new AppConfig();

            config.Normalize();
            return config;
        }
        catch (JsonException)
        {
            return await RecoverBrokenConfigAsync("invalid-json").ConfigureAwait(false);
        }
    }

    public async Task SaveAsync(AppConfig config, CancellationToken ct = default)
    {
        config.Normalize();
        Directory.CreateDirectory(ConfigDirectory);

        await _saveLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var stream = File.Create(ConfigPath);
            await JsonSerializer.SerializeAsync(stream, config, _jsonOptions, ct).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async Task SaveTranscriptAsync(string path, ChatTranscript transcript, CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, transcript, _jsonOptions, ct).ConfigureAwait(false);
    }

    public async Task<ChatTranscript> LoadTranscriptAsync(string path, CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Файл диалога не найден.", fullPath);
        }

        await using var stream = File.OpenRead(fullPath);
        var transcript = await JsonSerializer.DeserializeAsync<ChatTranscript>(stream, _jsonOptions, ct).ConfigureAwait(false);
        if (transcript is null)
        {
            throw new InvalidOperationException("Не удалось разобрать файл диалога.");
        }

        transcript.Messages ??= new List<ChatMessage>();
        transcript.Provider = string.IsNullOrWhiteSpace(transcript.Provider) ? "openai" : transcript.Provider.Trim().ToLowerInvariant();
        transcript.Model = string.IsNullOrWhiteSpace(transcript.Model) ? "gpt-5.1" : transcript.Model.Trim();

        return transcript;
    }

    private async Task<AppConfig> RecoverBrokenConfigAsync(string reason)
    {
        var recoveredConfig = new AppConfig();
        recoveredConfig.Normalize();

        if (File.Exists(ConfigPath))
        {
            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            var backupPath = Path.Combine(ConfigDirectory, $"config.{reason}.{stamp}.broken.json");
            File.Copy(ConfigPath, backupPath, overwrite: true);
        }

        await SaveAsync(recoveredConfig).ConfigureAwait(false);
        return recoveredConfig;
    }
}

