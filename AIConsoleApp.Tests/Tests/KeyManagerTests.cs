using AIConsoleApp.Models;
using AIConsoleApp.Services;
using Xunit;

namespace AIConsoleApp.Tests.Tests;

public sealed class KeyManagerTests
{
    [Fact]
    public void AddKey_FirstKeyBecomesActive_AndSetActiveReordersLookup()
    {
        var config = new AppConfig();
        var manager = new KeyManager(config);

        Assert.True(manager.AddKey("openai", "sk-first"));
        Assert.True(manager.AddKey("openai", "sk-second"));
        Assert.True(manager.SetActiveKey("openai", "sk-second"));

        var ordered = manager.GetOrderedKeys("openai");
        var listed = manager.ListKeys();

        Assert.Equal(new[] { "sk-second", "sk-first" }, ordered);
        Assert.Contains(listed, item => item.Provider == "openai" && item.MaskedKey == "sk-second" && item.IsActive);
    }

    [Fact]
    public void DeleteActiveKey_PromotesRemainingKey()
    {
        var config = new AppConfig
        {
            ProviderKeys = new Dictionary<string, List<ProviderKeyEntry>>(StringComparer.OrdinalIgnoreCase)
            {
                ["openai"] =
                [
                    new ProviderKeyEntry { Key = "sk-first", IsActive = false, AddedAt = DateTimeOffset.UtcNow.AddMinutes(-2) },
                    new ProviderKeyEntry { Key = "sk-second", IsActive = true, AddedAt = DateTimeOffset.UtcNow.AddMinutes(-1) }
                ]
            }
        };
        var manager = new KeyManager(config);

        Assert.True(manager.DeleteKey("openai", "sk-second"));

        var listed = manager.ListKeys();
        Assert.Single(listed);
        Assert.True(listed[0].IsActive);
        Assert.Equal("sk-first", manager.GetOrderedKeys("openai")[0]);
    }
}
