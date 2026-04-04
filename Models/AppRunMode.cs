namespace AIConsoleApp.Models;

public static class AppRunMode
{
    public const string Plan = "plan";
    public const string Build = "build";
    public const string Autopilot = "autopilot";

    public static string Normalize(string? mode)
    {
        return (mode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            Plan => Plan,
            Autopilot => Autopilot,
            _ => Build
        };
    }
}
