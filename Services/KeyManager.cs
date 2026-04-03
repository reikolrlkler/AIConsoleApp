using AIConsoleApp.Models;

namespace AIConsoleApp.Services;

public sealed class KeyManager
{
    private readonly AppConfig _config;
    private readonly object _sync = new();

    public KeyManager(AppConfig config)
    {
        _config = config;
        _config.Normalize();
    }

    public bool AddKey(string provider, string key)
    {
        provider = ModelCatalog.NormalizeProvider(provider);
        key = key.Trim();

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        lock (_sync)
        {
            var entries = GetOrCreateEntries(provider);
            if (entries.Any(existing => string.Equals(existing.Key, key, StringComparison.Ordinal)))
            {
                return false;
            }

            var shouldBecomeActive = entries.Count == 0;
            entries.Add(new ProviderKeyEntry
            {
                Key = key,
                IsActive = shouldBecomeActive,
                AddedAt = DateTimeOffset.UtcNow
            });

            return true;
        }
    }

    public bool DeleteKey(string provider, string key)
    {
        provider = ModelCatalog.NormalizeProvider(provider);

        lock (_sync)
        {
            if (!_config.ProviderKeys.TryGetValue(provider, out var entries))
            {
                return false;
            }

            var removed = entries.RemoveAll(existing => string.Equals(existing.Key, key, StringComparison.Ordinal)) > 0;
            if (!removed)
            {
                return false;
            }

            if (entries.Count == 0)
            {
                _config.ProviderKeys.Remove(provider);
            }
            else if (entries.All(static entry => !entry.IsActive))
            {
                entries[0].IsActive = true;
            }

            return true;
        }
    }

    public bool DeleteAllKeys(string provider)
    {
        provider = ModelCatalog.NormalizeProvider(provider);

        lock (_sync)
        {
            return _config.ProviderKeys.Remove(provider);
        }
    }

    public bool SetActiveKey(string provider, string? key = null)
    {
        provider = ModelCatalog.NormalizeProvider(provider);

        lock (_sync)
        {
            if (!_config.ProviderKeys.TryGetValue(provider, out var entries) || entries.Count == 0)
            {
                return false;
            }

            ProviderKeyEntry? target = key is null
                ? entries[0]
                : entries.FirstOrDefault(existing => string.Equals(existing.Key, key, StringComparison.Ordinal));

            if (target is null)
            {
                return false;
            }

            foreach (var entry in entries)
            {
                entry.IsActive = false;
            }

            target.IsActive = true;
            return true;
        }
    }

    public void MarkKeySuccessful(string provider, string key)
    {
        provider = ModelCatalog.NormalizeProvider(provider);

        lock (_sync)
        {
            if (!_config.ProviderKeys.TryGetValue(provider, out var entries))
            {
                return;
            }

            var target = entries.FirstOrDefault(existing => string.Equals(existing.Key, key, StringComparison.Ordinal));
            if (target is null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                entry.IsActive = false;
            }

            target.IsActive = true;
        }
    }

    public IReadOnlyList<string> GetOrderedKeys(string provider)
    {
        provider = ModelCatalog.NormalizeProvider(provider);

        lock (_sync)
        {
            if (!_config.ProviderKeys.TryGetValue(provider, out var entries) || entries.Count == 0)
            {
                return Array.Empty<string>();
            }

            return entries
                .OrderByDescending(static entry => entry.IsActive)
                .ThenBy(static entry => entry.AddedAt)
                .Select(static entry => entry.Key)
                .ToList();
        }
    }

    public bool HasAnyKey(string provider)
    {
        provider = ModelCatalog.NormalizeProvider(provider);

        lock (_sync)
        {
            return _config.ProviderKeys.TryGetValue(provider, out var entries) && entries.Count > 0;
        }
    }

    public IReadOnlyList<(string Provider, string MaskedKey, bool IsActive)> ListKeys()
    {
        lock (_sync)
        {
            return _config.ProviderKeys
                .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .SelectMany(static pair => pair.Value.Select(entry => (
                    Provider: pair.Key,
                    MaskedKey: MaskKey(entry.Key),
                    IsActive: entry.IsActive)))
                .ToList();
        }
    }

    private List<ProviderKeyEntry> GetOrCreateEntries(string provider)
    {
        if (!_config.ProviderKeys.TryGetValue(provider, out var entries))
        {
            entries = new List<ProviderKeyEntry>();
            _config.ProviderKeys[provider] = entries;
        }

        return entries;
    }

    private static string MaskKey(string key)
    {
        if (key.Length <= 10)
        {
            return key;
        }

        return $"{key[..6]}...{key[^4..]}";
    }
}
