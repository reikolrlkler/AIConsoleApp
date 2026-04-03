using System.Text.Json;
using AIConsoleApp.Models;
using AIConsoleApp.Providers;

namespace AIConsoleApp.Services;

public static class AiActionPlanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<string?> TryExecuteAsync(
        string userInput,
        string workspaceRoot,
        string currentDirectory,
        List<ChatMessage> history,
        AiProvider provider,
        Func<string, string> resolvePath,
        Action<string> updateCurrentDirectory,
        CancellationToken ct)
    {
        var plannerPrompt = BuildPlannerPrompt(userInput, workspaceRoot, currentDirectory);
        var plannerResponse = await provider.SendMessageAsync(plannerPrompt, history, ct).ConfigureAwait(false);
        var plan = ParsePlan(plannerResponse);
        if (plan is null || !plan.RequiresAction || plan.Actions.Count == 0)
        {
            return null;
        }

        var outputs = new List<string>();
        foreach (var action in plan.Actions)
        {
            var result = await ExecuteActionAsync(action, currentDirectory, resolvePath, updateCurrentDirectory, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result))
            {
                outputs.Add(result);
                currentDirectory = GetUpdatedDirectory(action, currentDirectory, resolvePath);
            }
        }

        if (outputs.Count == 0)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(plan.Summary)
            ? string.Join(Environment.NewLine, outputs)
            : $"{plan.Summary}{Environment.NewLine}{string.Join(Environment.NewLine, outputs)}";
    }

    private static string BuildPlannerPrompt(string userInput, string workspaceRoot, string currentDirectory)
    {
        return string.Join(Environment.NewLine,
            "You are a planning layer for a CLI coding assistant.",
            "Decide whether the user's request should trigger local workspace actions instead of a normal chat response.",
            string.Empty,
            "Rules:",
            "- Only plan actions when the user clearly asks to create a folder, create a file, write content, or change directory.",
            "- Never use paths outside the workspace root.",
            "- Supported action types: cd, mkdir, write_file.",
            "- For write_file, include the full file path relative to the workspace and the exact content to write.",
            "- If no local action is needed, return a JSON object with requiresAction=false.",
            "- Return JSON only. No markdown fences. No explanation.",
            string.Empty,
            $"Workspace root: {workspaceRoot}",
            $"Current directory: {currentDirectory}",
            string.Empty,
            "Return one of these shapes:",
            "{\"requiresAction\":false,\"summary\":\"\"}",
            "{\"requiresAction\":true,\"summary\":\"short summary\",\"actions\":[{\"type\":\"mkdir\",\"path\":\"demo\"},{\"type\":\"write_file\",\"path\":\"demo/hello.py\",\"content\":\"print('Hello, world!')\"}]}",
            string.Empty,
            "User request:",
            userInput);
    }

    private static AiActionPlan? ParsePlan(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var trimmed = response.Trim();
        if (trimmed.StartsWith("```") && trimmed.EndsWith("```"))
        {
            trimmed = trimmed.Trim('`').Trim();
            var newLineIndex = trimmed.IndexOf('\n');
            if (newLineIndex >= 0)
            {
                trimmed = trimmed[(newLineIndex + 1)..].Trim();
            }
        }

        try
        {
            return JsonSerializer.Deserialize<AiActionPlan>(trimmed, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> ExecuteActionAsync(
        AiActionStep action,
        string currentDirectory,
        Func<string, string> resolvePath,
        Action<string> updateCurrentDirectory,
        CancellationToken ct)
    {
        var type = (action.Type ?? string.Empty).Trim().ToLowerInvariant();
        switch (type)
        {
            case "cd":
            {
                var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, action.Path);
                Directory.CreateDirectory(fullPath);
                updateCurrentDirectory(fullPath);
                return $"Changed directory: {fullPath}";
            }
            case "mkdir":
            {
                var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, action.Path);
                Directory.CreateDirectory(fullPath);
                return $"Created folder: {fullPath}";
            }
            case "write_file":
            {
                var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, action.Path);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(fullPath, action.Content ?? string.Empty, ct).ConfigureAwait(false);
                return $"Wrote file: {fullPath}";
            }
            default:
                return null;
        }
    }

    private static string ResolveAgainstCurrent(Func<string, string> resolvePath, string currentDirectory, string path)
    {
        var combined = Path.IsPathRooted(path) ? path : Path.Combine(currentDirectory, path ?? string.Empty);
        return resolvePath(combined);
    }

    private static string GetUpdatedDirectory(AiActionStep action, string currentDirectory, Func<string, string> resolvePath)
    {
        if (!string.Equals(action.Type, "cd", StringComparison.OrdinalIgnoreCase))
        {
            return currentDirectory;
        }

        return ResolveAgainstCurrent(resolvePath, currentDirectory, action.Path);
    }
}
