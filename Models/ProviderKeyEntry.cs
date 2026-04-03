namespace AIConsoleApp.Models;

public sealed class ProviderKeyEntry
{
    public string Key { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
}
