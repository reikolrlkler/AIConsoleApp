using AIConsoleApp.Services;
using Xunit;

namespace AIConsoleApp.Tests.Tests;

public sealed class LocalActionInterpreterTests
{
    [Fact]
    public async Task TryExecuteAsync_CreatesFolder_FromNaturalLanguage()
    {
        var root = Path.Combine(Path.GetTempPath(), "AIConsoleTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string currentDirectory = root;
            string Resolve(string rawPath) => Path.GetFullPath(Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(root, rawPath));
            void Update(string directory) => currentDirectory = directory;

            var result = await LocalActionInterpreter.TryExecuteAsync("create folder demo_nl", currentDirectory, Resolve, Update);

            Assert.NotNull(result);
            Assert.True(Directory.Exists(Path.Combine(root, "demo_nl")));
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
    public async Task TryExecuteAsync_CreatesFileAndWritesContent_FromNaturalLanguage()
    {
        var root = Path.Combine(Path.GetTempPath(), "AIConsoleTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string currentDirectory = root;
            string Resolve(string rawPath) => Path.GetFullPath(Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(root, rawPath));
            void Update(string directory) => currentDirectory = directory;

            var result = await LocalActionInterpreter.TryExecuteAsync(
                "create file demo_nl\\hello.py and write there print('Hello, world!')",
                currentDirectory,
                Resolve,
                Update);

            var filePath = Path.Combine(root, "demo_nl", "hello.py");
            Assert.NotNull(result);
            Assert.True(File.Exists(filePath));
            Assert.Contains("Hello, world!", await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
