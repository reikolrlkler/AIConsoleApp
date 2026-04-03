namespace AIConsoleApp.Models;

public sealed class ModelDefinition
{
    public string Provider { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool RequiresApiKey { get; init; } = true;

    public bool SupportsStreaming { get; init; } = true;

    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

    public string? Notes { get; init; }

    public bool Matches(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(ModelId, value, StringComparison.OrdinalIgnoreCase)
            || string.Equals(DisplayName, value, StringComparison.OrdinalIgnoreCase)
            || Aliases.Any(alias => string.Equals(alias, value, StringComparison.OrdinalIgnoreCase));
    }
}
