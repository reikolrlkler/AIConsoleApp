namespace AIConsoleApp.Models;

public sealed class AiActionPlan
{
    public bool RequiresAction { get; set; }

    public string Summary { get; set; } = string.Empty;

    public List<AiActionStep> Actions { get; set; } = new();
}

public sealed class AiActionStep
{
    public string Type { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
