using AIConsoleApp.Models;
using Xunit;

namespace AIConsoleApp.Tests.Tests;

public sealed class AppConfigTests
{
    [Fact]
    public void Normalize_FixesDefaults_ActivatesFirstKey_AndDeduplicatesDiscoveredModels()
    {
        var config = new AppConfig
        {
            Language = " Russian ",
            ActiveProvider = "  ",
            ActiveModel = "",
            RequestTimeoutSeconds = 0,
            MaxRetriesPerKey = -1,
            ProviderKeys = new Dictionary<string, List<ProviderKeyEntry>>(StringComparer.OrdinalIgnoreCase)
            {
                [" OpenAI "] =
                [
                    new ProviderKeyEntry { Key = " sk-one ", IsActive = false, AddedAt = default },
                    new ProviderKeyEntry { Key = "sk-two", IsActive = false, AddedAt = DateTimeOffset.UtcNow }
                ]
            },
            DiscoveredModels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [" OpenAI "] = [" gpt-4o ", "gpt-4o", "gpt-4-turbo"]
            }
        };

        config.Normalize();

        Assert.Equal("ru", config.Language);
        Assert.Equal("openai", config.ActiveProvider);
        Assert.Equal("gpt-5.1", config.ActiveModel);
        Assert.Equal(90, config.RequestTimeoutSeconds);
        Assert.Equal(0, config.MaxRetriesPerKey);
        Assert.True(config.ProviderKeys["openai"][0].IsActive);
        Assert.Equal(new[] { "gpt-4-turbo", "gpt-4o" }, config.DiscoveredModels["openai"]);
    }
}
