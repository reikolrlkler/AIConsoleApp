using AIConsoleApp.Models;
using Xunit;

namespace AIConsoleApp.Tests.Tests;

public sealed class AppConfigTests
{
    [Fact]
    public void Normalize_FillsMissingSession_AndDefaultsRunMode()
    {
        var config = new AppConfig
        {
            RunMode = "",
            CurrentSessionName = "default",
            ChatHistory =
            [
                new ChatMessage { Role = "user", Content = "hi", Timestamp = DateTimeOffset.UtcNow }
            ]
        };

        config.Normalize();

        Assert.Equal(AppRunMode.Build, config.RunMode);
        Assert.True(config.Sessions.ContainsKey("default"));
        Assert.Single(config.ChatHistory);
    }
}
