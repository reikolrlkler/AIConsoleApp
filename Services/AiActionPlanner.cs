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

        return await ToolExecutor.ExecutePlanAsync(plan, workspaceRoot, currentDirectory, resolvePath, updateCurrentDirectory, ct).ConfigureAwait(false);
    }

    private static string BuildPlannerPrompt(string userInput, string workspaceRoot, string currentDirectory)
    {
        return string.Join(Environment.NewLine,
            "You are a planning layer for a CLI coding assistant.",
            "Your only job is to decide whether the user request should execute tool actions instead of a normal chat response.",
            string.Empty,
            "Rules:",
            "- Only plan actions when the user is clearly asking to inspect files, search, fetch URLs, run project commands, change directory, or create/edit files.",
            "- Never use paths outside the workspace root.",
            "- Prefer simple, minimal actions.",
            "- For file writes, include exact content.",
            "- If no tool action is needed, return requiresAction=false.",
            "- Return valid JSON only. No markdown. No extra words.",
            string.Empty,
            "Allowed actions:",
            string.Join(", ", ToolExecutor.GetTools().Select(static tool => tool.Name)),
            string.Empty,
            $"Workspace root: {workspaceRoot}",
            $"Current directory: {currentDirectory}",
            string.Empty,
            "JSON shapes:",
            "{\"requiresAction\":false,\"summary\":\"\",\"actions\":[]}",
            "{\"requiresAction\":true,\"summary\":\"Created a folder.\",\"actions\":[{\"action\":\"mkdir\",\"args\":{\"path\":\"new-folder\"}}]}",
            "{\"requiresAction\":true,\"summary\":\"Searched the workspace.\",\"actions\":[{\"action\":\"grep\",\"args\":{\"pattern\":\"TODO\",\"path\":\".\",\"recursive\":\"true\"}}]}",
            string.Empty,
            "User request:",
            userInput);
    }

    private static ToolActionPlan? ParsePlan(string response)
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

        var objectStart = trimmed.IndexOf('{');
        var objectEnd = trimmed.LastIndexOf('}');
        if (objectStart >= 0 && objectEnd > objectStart)
        {
            trimmed = trimmed[objectStart..(objectEnd + 1)];
        }

        try
        {
            return JsonSerializer.Deserialize<ToolActionPlan>(trimmed, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
