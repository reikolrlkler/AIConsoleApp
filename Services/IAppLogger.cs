namespace AIConsoleApp.Services;

public interface IAppLogger
{
    Task InfoAsync(string message, CancellationToken ct = default);

    Task WarningAsync(string message, CancellationToken ct = default);

    Task ErrorAsync(string message, CancellationToken ct = default);
}
