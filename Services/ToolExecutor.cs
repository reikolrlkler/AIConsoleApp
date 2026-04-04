using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AIConsoleApp.Models;

namespace AIConsoleApp.Services;

public static class ToolExecutor
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static readonly IReadOnlyList<(string Name, string Description)> ToolList =
    [
        ("pwd", "show current working directory"),
        ("cd", "change working directory"),
        ("ls", "list files and folders"),
        ("glob", "glob search inside workspace"),
        ("grep", "search text in files"),
        ("read_file", "read a file"),
        ("write_file", "write a file"),
        ("append_file", "append text to a file"),
        ("mkdir", "create a directory"),
        ("move", "move or rename file/directory"),
        ("copy", "copy file or directory"),
        ("delete_file", "delete a file"),
        ("delete_dir", "delete a directory"),
        ("stat", "show file or directory info"),
        ("fetch", "read URL content"),
        ("web_search", "search the web"),
        ("shell", "run shell command"),
        ("npm_install", "install npm package"),
        ("npm_uninstall", "remove npm package"),
        ("npm_run", "run npm script"),
        ("pip_install", "install pip package"),
        ("pip_uninstall", "remove pip package"),
        ("dotnet_restore", "run dotnet restore"),
        ("dotnet_build", "run dotnet build"),
        ("dotnet_test", "run dotnet test"),
        ("dotnet_publish", "run dotnet publish")
    ];

    public static IReadOnlyList<(string Name, string Description)> GetTools() => ToolList;

    public static string FormatToolList()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Available tools:");
        foreach (var tool in ToolList)
        {
            builder.AppendLine($"- {tool.Name,-14} {tool.Description}");
        }

        return builder.ToString().TrimEnd();
    }

    public static async Task<string?> ExecutePlanAsync(
        ToolActionPlan plan,
        string workspaceRoot,
        string currentDirectory,
        Func<string, string> resolvePath,
        Action<string> updateCurrentDirectory,
        CancellationToken ct)
    {
        if (!plan.RequiresAction || plan.Actions.Count == 0)
        {
            return null;
        }

        var outputs = new List<string>();
        foreach (var action in plan.Actions)
        {
            var result = await ExecuteStepAsync(action, workspaceRoot, currentDirectory, resolvePath, updateCurrentDirectory, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result))
            {
                outputs.Add(result);
                if (string.Equals(action.Action, "cd", StringComparison.OrdinalIgnoreCase) && action.Args.TryGetValue("path", out var nextPath))
                {
                    currentDirectory = ResolveAgainstCurrent(resolvePath, currentDirectory, nextPath);
                }
            }
        }

        if (outputs.Count == 0)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(plan.Summary)
            ? string.Join(Environment.NewLine + Environment.NewLine, outputs)
            : $"{plan.Summary}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine + Environment.NewLine, outputs)}";
    }

    public static Task<string?> ExecuteNamedActionAsync(
        string action,
        IReadOnlyDictionary<string, string> args,
        string workspaceRoot,
        string currentDirectory,
        Func<string, string> resolvePath,
        Action<string> updateCurrentDirectory,
        CancellationToken ct)
    {
        var step = new ToolActionStep
        {
            Action = action,
            Args = args.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };

        return ExecuteStepAsync(step, workspaceRoot, currentDirectory, resolvePath, updateCurrentDirectory, ct);
    }

    public static async Task<string?> ExecuteStepAsync(
        ToolActionStep step,
        string workspaceRoot,
        string currentDirectory,
        Func<string, string> resolvePath,
        Action<string> updateCurrentDirectory,
        CancellationToken ct)
    {
        var action = (step.Action ?? string.Empty).Trim().ToLowerInvariant();
        var args = step.Args ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return action switch
        {
            "pwd" => currentDirectory,
            "cd" => ExecuteChangeDirectory(args, currentDirectory, resolvePath, updateCurrentDirectory),
            "ls" => ExecuteListFiles(args, currentDirectory, resolvePath),
            "glob" => ExecuteGlob(args, workspaceRoot, currentDirectory, resolvePath),
            "grep" => ExecuteGrep(args, workspaceRoot, currentDirectory, resolvePath),
            "read_file" => await ExecuteReadFileAsync(args, currentDirectory, resolvePath, ct).ConfigureAwait(false),
            "write_file" => await ExecuteWriteFileAsync(args, currentDirectory, resolvePath, append: false, ct).ConfigureAwait(false),
            "append_file" => await ExecuteWriteFileAsync(args, currentDirectory, resolvePath, append: true, ct).ConfigureAwait(false),
            "mkdir" => ExecuteMakeDirectory(args, currentDirectory, resolvePath),
            "move" => ExecuteMove(args, currentDirectory, resolvePath),
            "copy" => ExecuteCopy(args, currentDirectory, resolvePath),
            "delete_file" => ExecuteDeleteFile(args, currentDirectory, resolvePath),
            "delete_dir" => ExecuteDeleteDirectory(args, currentDirectory, resolvePath),
            "stat" => ExecuteStat(args, currentDirectory, resolvePath),
            "fetch" => await ExecuteFetchAsync(args, ct).ConfigureAwait(false),
            "web_search" => await ExecuteWebSearchAsync(args, ct).ConfigureAwait(false),
            "shell" => await ExecuteShellAsync(args, currentDirectory, ct).ConfigureAwait(false),
            "npm_install" => await ExecuteShellProcessAsync("npm", $"install {RequireArg(args, "package")}", currentDirectory, ct).ConfigureAwait(false),
            "npm_uninstall" => await ExecuteShellProcessAsync("npm", $"uninstall {RequireArg(args, "package")}", currentDirectory, ct).ConfigureAwait(false),
            "npm_run" => await ExecuteShellProcessAsync("npm", $"run {RequireArg(args, "script")}", currentDirectory, ct).ConfigureAwait(false),
            "pip_install" => await ExecuteShellProcessAsync("python", $"-m pip install {RequireArg(args, "package")}", currentDirectory, ct).ConfigureAwait(false),
            "pip_uninstall" => await ExecuteShellProcessAsync("python", $"-m pip uninstall -y {RequireArg(args, "package")}", currentDirectory, ct).ConfigureAwait(false),
            "dotnet_restore" => await ExecuteShellProcessAsync("dotnet", "restore", currentDirectory, ct).ConfigureAwait(false),
            "dotnet_build" => await ExecuteShellProcessAsync("dotnet", "build", currentDirectory, ct).ConfigureAwait(false),
            "dotnet_test" => await ExecuteShellProcessAsync("dotnet", "test", currentDirectory, ct).ConfigureAwait(false),
            "dotnet_publish" => await ExecuteShellProcessAsync("dotnet", "publish", currentDirectory, ct).ConfigureAwait(false),
            _ => $"Unknown tool action: {action}"
        };
    }

    private static string ExecuteChangeDirectory(IReadOnlyDictionary<string, string> args, string currentDirectory, Func<string, string> resolvePath, Action<string> updateCurrentDirectory)
    {
        var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "path"));
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(fullPath);
        }

        updateCurrentDirectory(fullPath);
        return $"Changed directory: {fullPath}";
    }

    private static string ExecuteListFiles(IReadOnlyDictionary<string, string> args, string currentDirectory, Func<string, string> resolvePath)
    {
        var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, GetArg(args, "path", "."));
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(fullPath);
        }

        var entries = Directory.GetFileSystemEntries(fullPath)
            .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .Select(static item => $"[{(Directory.Exists(item) ? "dir" : "file")}] {Path.GetFileName(item)}")
            .ToList();

        return entries.Count == 0 ? $"No entries in {fullPath}" : string.Join(Environment.NewLine, entries);
    }

    private static string ExecuteGlob(IReadOnlyDictionary<string, string> args, string workspaceRoot, string currentDirectory, Func<string, string> resolvePath)
    {
        var pattern = GetArg(args, "pattern", "*");
        var basePath = ResolveAgainstCurrent(resolvePath, currentDirectory, GetArg(args, "path", "."));
        var recursive = GetArg(args, "recursive", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var matches = Directory.EnumerateFileSystemEntries(basePath, "*", options)
            .Where(path => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, Path.GetRelativePath(basePath, path)))
            .Take(100)
            .Select(path => Path.GetRelativePath(workspaceRoot, path))
            .ToList();

        return matches.Count == 0 ? $"No matches for pattern '{pattern}'." : string.Join(Environment.NewLine, matches);
    }

    private static string ExecuteGrep(IReadOnlyDictionary<string, string> args, string workspaceRoot, string currentDirectory, Func<string, string> resolvePath)
    {
        var pattern = RequireArg(args, "pattern");
        var basePath = ResolveAgainstCurrent(resolvePath, currentDirectory, GetArg(args, "path", "."));
        var recursive = GetArg(args, "recursive", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var results = new List<string>();

        foreach (var file in Directory.EnumerateFiles(basePath, "*", options).Take(300))
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add($"{Path.GetRelativePath(workspaceRoot, file)}:{lineNumber}: {line.Trim()}");
                    if (results.Count >= 100)
                    {
                        return string.Join(Environment.NewLine, results);
                    }
                }
            }
        }

        return results.Count == 0 ? $"No matches for '{pattern}'." : string.Join(Environment.NewLine, results);
    }

    private static async Task<string> ExecuteReadFileAsync(IReadOnlyDictionary<string, string> args, string currentDirectory, Func<string, string> resolvePath, CancellationToken ct)
    {
        var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "path"));
        var content = await File.ReadAllTextAsync(fullPath, ct).ConfigureAwait(false);
        return $"File: {fullPath}{Environment.NewLine}{Environment.NewLine}{content}";
    }

    private static async Task<string> ExecuteWriteFileAsync(IReadOnlyDictionary<string, string> args, string currentDirectory, Func<string, string> resolvePath, bool append, CancellationToken ct)
    {
        var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "path"));
        var content = GetArg(args, "content", string.Empty);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (append)
        {
            await File.AppendAllTextAsync(fullPath, content, ct).ConfigureAwait(false);
            return $"Appended file: {fullPath}";
        }

        await File.WriteAllTextAsync(fullPath, content, ct).ConfigureAwait(false);
        return $"Wrote file: {fullPath}";
    }

    private static string ExecuteMakeDirectory(IReadOnlyDictionary<string, string> args, string currentDirectory, Func<string, string> resolvePath)
    {
        var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "path"));
        Directory.CreateDirectory(fullPath);
        return $"Created folder: {fullPath}";
    }

    private static string ExecuteMove(IReadOnlyDictionary<string, string> args, string currentDirectory, Func<string, string> resolvePath)
    {
        var source = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "source"));
        var target = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "target"));
        var targetDirectory = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        if (Directory.Exists(source))
        {
            Directory.Move(source, target);
        }
        else
        {
            File.Move(source, target, overwrite: true);
        }

        return $"Moved: {source} -> {target}";
    }

    private static string ExecuteCopy(IReadOnlyDictionary<string, string> args, string currentDirectory, Func<string, string> resolvePath)
    {
        var source = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "source"));
        var target = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "target"));
        if (File.Exists(source))
        {
            var targetDirectory = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(source, target, overwrite: true);
            return $"Copied file: {source} -> {target}";
        }

        CopyDirectory(source, target);
        return $"Copied directory: {source} -> {target}";
    }

    private static string ExecuteDeleteFile(IReadOnlyDictionary<string, string> args, string currentDirectory, Func<string, string> resolvePath)
    {
        var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "path"));
        File.Delete(fullPath);
        return $"Deleted file: {fullPath}";
    }

    private static string ExecuteDeleteDirectory(IReadOnlyDictionary<string, string> args, string currentDirectory, Func<string, string> resolvePath)
    {
        var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "path"));
        Directory.Delete(fullPath, recursive: true);
        return $"Deleted directory: {fullPath}";
    }

    private static string ExecuteStat(IReadOnlyDictionary<string, string> args, string currentDirectory, Func<string, string> resolvePath)
    {
        var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, RequireArg(args, "path"));
        if (Directory.Exists(fullPath))
        {
            var directory = new DirectoryInfo(fullPath);
            return $"Directory: {directory.FullName}{Environment.NewLine}Created: {directory.CreationTimeUtc:u}{Environment.NewLine}Updated: {directory.LastWriteTimeUtc:u}";
        }

        var file = new FileInfo(fullPath);
        return $"File: {file.FullName}{Environment.NewLine}Size: {file.Length} bytes{Environment.NewLine}Created: {file.CreationTimeUtc:u}{Environment.NewLine}Updated: {file.LastWriteTimeUtc:u}";
    }

    private static async Task<string> ExecuteFetchAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        var url = RequireArg(args, "url");
        var text = await HttpClient.GetStringAsync(url, ct).ConfigureAwait(false);
        text = TrimLargeText(text, 4000);
        return $"Fetched: {url}{Environment.NewLine}{Environment.NewLine}{text}";
    }

    private static async Task<string> ExecuteWebSearchAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        var query = RequireArg(args, "query");
        var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
        var html = await HttpClient.GetStringAsync(url, ct).ConfigureAwait(false);
        var matches = Regex.Matches(html, "<a[^>]*class=\"result__a\"[^>]*href=\"(?<href>[^\"]+)\"[^>]*>(?<title>.*?)</a>", RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Take(5)
            .Select(match =>
            {
                var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
                var title = Regex.Replace(WebUtility.HtmlDecode(match.Groups["title"].Value), "<.*?>", string.Empty);
                return $"- {title}{Environment.NewLine}  {href}";
            })
            .ToList();

        return matches.Count == 0
            ? $"No web search results for '{query}'."
            : $"Web search: {query}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, matches)}";
    }

    private static Task<string> ExecuteShellAsync(IReadOnlyDictionary<string, string> args, string currentDirectory, CancellationToken ct)
    {
        var command = RequireArg(args, "command");
        return ExecuteShellProcessAsync("powershell", $"-NoProfile -Command {command}", currentDirectory, ct);
    }

    private static async Task<string> ExecuteShellProcessAsync(string fileName, string arguments, string workingDirectory, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        var text = string.IsNullOrWhiteSpace(error) ? output : $"{output}{Environment.NewLine}{error}";
        text = TrimLargeText(text, 4000);
        return $"$ {fileName} {arguments}{Environment.NewLine}{Environment.NewLine}{text}".TrimEnd();
    }

    private static string ResolveAgainstCurrent(Func<string, string> resolvePath, string currentDirectory, string path)
    {
        var combined = Path.IsPathRooted(path) ? path : Path.Combine(currentDirectory, path);
        return resolvePath(combined);
    }

    private static string RequireArg(IReadOnlyDictionary<string, string> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required argument: {key}");
        }

        return value;
    }

    private static string GetArg(IReadOnlyDictionary<string, string> args, string key, string fallback)
    {
        return args.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    private static string TrimLargeText(string value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxChars)
        {
            return value;
        }

        return value[..maxChars] + Environment.NewLine + "...";
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AIConsoleApp/1.0.0");
        return client;
    }
}


