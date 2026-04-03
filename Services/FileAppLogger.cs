using System.Text;

namespace AIConsoleApp.Services;

public sealed class FileAppLogger : IAppLogger
{
    private readonly SemaphoreSlim _sync = new(1, 1);

    public FileAppLogger(string baseDirectory)
    {
        LogDirectory = Path.Combine(baseDirectory, "logs");
        Directory.CreateDirectory(LogDirectory);
    }

    public string LogDirectory { get; }

    public Task InfoAsync(string message, CancellationToken ct = default)
    {
        return WriteAsync("INFO", message, ct);
    }

    public Task WarningAsync(string message, CancellationToken ct = default)
    {
        return WriteAsync("WARN", message, ct);
    }

    public Task ErrorAsync(string message, CancellationToken ct = default)
    {
        return WriteAsync("ERROR", message, ct);
    }

    private async Task WriteAsync(string level, string message, CancellationToken ct)
    {
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{level}] {message}{Environment.NewLine}";
        var path = Path.Combine(LogDirectory, $"aiconsole-{DateTime.UtcNow:yyyy-MM-dd}.log");

        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(path, line, Encoding.UTF8, ct).ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
    }
}
