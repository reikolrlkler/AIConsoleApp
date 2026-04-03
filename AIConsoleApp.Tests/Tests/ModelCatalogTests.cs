using AIConsoleApp.Services;
using Xunit;

namespace AIConsoleApp.Tests.Tests;

public sealed class ModelCatalogTests
{
    [Theory]
    [InlineData("grok", "groq")]
    [InlineData("claude", "anthropic")]
    [InlineData("gemini", "google")]
    [InlineData("openai", "openai")]
    public void NormalizeProvider_MapsAliases(string input, string expected)
    {
        Assert.Equal(expected, ModelCatalog.NormalizeProvider(input));
    }

    [Fact]
    public void ResolveModel_ReturnsProviderDefaultWhenProviderSpecified()
    {
        var model = ModelCatalog.ResolveModel("mistral", null);

        Assert.Equal("mistral", model.Provider);
        Assert.Equal("mistral-large-latest", model.ModelId);
    }

    [Fact]
    public void GetModelsForProvider_ReturnsKnownEntries()
    {
        var models = ModelCatalog.GetModelsForProvider("openai");

        Assert.Contains(models, item => item.ModelId == "gpt-5.1");
        Assert.Contains(models, item => item.ModelId == "gpt-4.1");
    }
}
