using AIConsoleApp.Services;
using Xunit;

namespace AIConsoleApp.Tests.Tests;

public sealed class CommandTokenizerTests
{
    [Fact]
    public void Tokenize_RespectsQuotedSegments()
    {
        var tokens = CommandTokenizer.Tokenize("/ask provider=groq model=llama3-70b-8192 \"hello world\"");

        Assert.Equal(new[]
        {
            "/ask",
            "provider=groq",
            "model=llama3-70b-8192",
            "hello world"
        }, tokens);
    }

    [Fact]
    public void Tokenize_ThrowsOnUnclosedQuote()
    {
        Assert.Throws<InvalidOperationException>(() => CommandTokenizer.Tokenize("/ask \"broken"));
    }

    [Fact]
    public void ParseNamedArguments_ExtractsKeyValuePairs()
    {
        var args = CommandTokenizer.ParseNamedArguments(new[]
        {
            "provider=openai",
            "model=gpt-4o",
            "ignored"
        });

        Assert.Equal("openai", args["provider"]);
        Assert.Equal("gpt-4o", args["model"]);
        Assert.DoesNotContain("ignored", args.Keys);
    }
}
