namespace AIConsoleApp.Models;

public sealed class AppConfig
{
    public string Language { get; set; } = string.Empty;

    public string ActiveProvider { get; set; } = "openai";

    public string ActiveModel { get; set; } = "gpt-5.1";

    public bool StreamingEnabled { get; set; } = true;

    public int RequestTimeoutSeconds { get; set; } = 90;

    public int MaxRetriesPerKey { get; set; } = 2;

    public Dictionary<string, List<ProviderKeyEntry>> ProviderKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<string>> DiscoveredModels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<ChatMessage> ChatHistory { get; set; } = new();

    public void Normalize()
    {
        Language = string.IsNullOrWhiteSpace(Language) ? string.Empty : Services.AppLanguage.Normalize(Language);
        ActiveProvider = string.IsNullOrWhiteSpace(ActiveProvider) ? "openai" : ActiveProvider.Trim().ToLowerInvariant();
        ActiveModel = string.IsNullOrWhiteSpace(ActiveModel) ? "gpt-5.1" : ActiveModel.Trim();
        RequestTimeoutSeconds = RequestTimeoutSeconds <= 0 ? 90 : RequestTimeoutSeconds;
        MaxRetriesPerKey = MaxRetriesPerKey < 0 ? 0 : MaxRetriesPerKey;
        ProviderKeys ??= new Dictionary<string, List<ProviderKeyEntry>>(StringComparer.OrdinalIgnoreCase);
        DiscoveredModels ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        ChatHistory ??= new List<ChatMessage>();

        var normalized = new Dictionary<string, List<ProviderKeyEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in ProviderKeys)
        {
            var provider = string.IsNullOrWhiteSpace(pair.Key) ? string.Empty : pair.Key.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(provider))
            {
                continue;
            }

            var keys = pair.Value ?? new List<ProviderKeyEntry>();
            normalized[provider] = keys
                .Where(static entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Key))
                .Select(static entry => new ProviderKeyEntry
                {
                    Key = entry.Key.Trim(),
                    IsActive = entry.IsActive,
                    AddedAt = entry.AddedAt == default ? DateTimeOffset.UtcNow : entry.AddedAt
                })
                .ToList();

            if (normalized[provider].Count > 0 && normalized[provider].All(static entry => !entry.IsActive))
            {
                normalized[provider][0].IsActive = true;
            }
        }

        ProviderKeys = normalized;
        DiscoveredModels = DiscoveredModels
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                static pair => pair.Key.Trim().ToLowerInvariant(),
                static pair => (pair.Value ?? new List<string>())
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
        ChatHistory = ChatHistory
            .Where(static message => message is not null && !string.IsNullOrWhiteSpace(message.Role))
            .Select(static message => message.Clone())
            .ToList();
    }
}
