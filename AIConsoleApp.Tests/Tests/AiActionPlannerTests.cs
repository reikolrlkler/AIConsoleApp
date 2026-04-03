using AIConsoleApp.Models;
using AIConsoleApp.Services;
using Xunit;

namespace AIConsoleApp.Tests.Tests;

public sealed class AiActionPlannerTests
{
    [Fact]
    public async Task TryExecuteAsync_ExecutesJsonPlan_FromPlannerResponse()
    {
        var root = Path.Combine(Path.GetTempPath(), "AIConsolePlannerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var json = "{\"requiresAction\":true,\"summary\":\"done\",\"actions\":[{\"type\":\"mkdir\",\"path\":\"demo\"},{\"type\":\"write_file\",\"path\":\"demo/hello.py\",\"content\":\"print('Hello, world!')\"}]}";
            var provider = new FakeProvider(json);
            var history = new List<ChatMessage>();
            var currentDirectory = root;
            string Resolve(string rawPath) => Path.GetFullPath(Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(root, rawPath));
            void Update(string directory) => currentDirectory = directory;

            var result = await AiActionPlanner.TryExecuteAsync("create python file", root, currentDirectory, history, provider, Resolve, Update, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(Directory.Exists(Path.Combine(root, "demo")));
            Assert.Contains("Hello, world!", await File.ReadAllTextAsync(Path.Combine(root, "demo", "hello.py")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task TryExecuteAsync_ParsesJsonWrappedInExtraText()
    {
        var root = Path.Combine(Path.GetTempPath(), "AIConsolePlannerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var response = "Here is the plan: {\"requiresAction\":true,\"summary\":\"Created a folder.\",\"actions\":[{\"type\":\"mkdir\",\"path\":\"new-folder\"}]} done.";
            var provider = new FakeProvider(response);
            var history = new List<ChatMessage>();
            var currentDirectory = root;
            string Resolve(string rawPath) => Path.GetFullPath(Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(root, rawPath));
            void Update(string directory) => currentDirectory = directory;

            var result = await AiActionPlanner.TryExecuteAsync("hi create folder", root, currentDirectory, history, provider, Resolve, Update, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(Directory.Exists(Path.Combine(root, "new-folder")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private sealed class FakeProvider : Providers.AiProvider
    {
        private readonly string _response;

        public FakeProvider(string response)
            : base("fake", "fake-model", new KeyManager(new AppConfig()), new HttpClient(), new ProviderRuntimeOptions(), new NullLogger(), requiresApiKey: false)
        {
            _response = response;
        }

        public override Task<string> SendMessageAsync(string message, List<ChatMessage> history, CancellationToken ct)
        {
            return Task.FromResult(_response);
        }

        public override async IAsyncEnumerable<string> StreamMessageAsync(string message, List<ChatMessage> history, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            yield return _response;
            await Task.CompletedTask;
        }
    }

    private sealed class NullLogger : IAppLogger
    {
        public Task InfoAsync(string message, CancellationToken ct = default) => Task.CompletedTask;
        public Task WarningAsync(string message, CancellationToken ct = default) => Task.CompletedTask;
        public Task ErrorAsync(string message, CancellationToken ct = default) => Task.CompletedTask;
    }
}
