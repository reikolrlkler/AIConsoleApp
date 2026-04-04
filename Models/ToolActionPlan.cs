namespace AIConsoleApp.Models;

public sealed class ToolActionPlan
{
    public bool RequiresAction { get; set; }

    public string Summary { get; set; } = string.Empty;

    public List<ToolActionStep> Actions { get; set; } = new();
}

public sealed class ToolActionStep
{
    public string Action { get; set; } = string.Empty;

    public Dictionary<string, string> Args { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
