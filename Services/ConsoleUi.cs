using System.Reflection;
using AIConsoleApp.Models;

namespace AIConsoleApp.Services;

public static class ConsoleUi
{
    private static readonly string[] Logo =
    [
        " █████╗ ██╗ ██████╗ ██████╗ ███╗   ██╗███████╗ ██████╗ ██╗     ███████╗ ",
        "██╔══██╗██║██╔════╝██╔═══██╗████╗  ██║██╔════╝██╔═══██╗██║     ██╔════╝ ",
        "███████║██║██║     ██║   ██║██╔██╗ ██║███████╗██║   ██║██║     █████╗   ",
        "██╔══██║██║██║     ██║   ██║██║╚██╗██║╚════██║██║   ██║██║     ██╔══╝   ",
        "██║  ██║██║╚██████╗╚██████╔╝██║ ╚████║███████║╚██████╔╝███████╗███████╗ ",
        "╚═╝  ╚═╝╚═╝ ╚═════╝ ╚═════╝ ╚═╝  ╚═══╝╚══════╝ ╚═════╝ ╚══════╝╚══════╝ "
    ];

    private static readonly (string Command, string Description)[] Shortcuts =
    [
        ("/help", "show commands"),
        ("/model list", "list models"),
        ("/chat list", "list sessions"),
        ("/doctor", "check runtime"),
        ("/clear", "clear chat")
    ];

    public static void PrintBanner(ConsoleText text, string configPath, string logDirectory, AppConfig config)
    {
        Console.Clear();
        var width = GetContentWidth();

        WriteTopChrome();
        Console.WriteLine();
        WriteCentered("aiconsole", ConsoleColor.Gray, width);
        Console.WriteLine();

        foreach (var line in Logo)
        {
            WriteCentered(line, ConsoleColor.White, width);
        }

        WriteCentered($"v{GetVersionText()}", ConsoleColor.DarkGray, width);
        Console.WriteLine();
        WriteShortcutGrid(width);
        Console.WriteLine();
        WriteStatusLine(config, width);
        WriteMutedCentered(text.BannerFooter, width);
        WriteRule('─', ConsoleColor.DarkGray, width);
        Console.WriteLine();
    }

    public static void WritePrompt(AppConfig config)
    {
        var width = Math.Clamp(GetContentWidth() - 10, 48, 96);
        var leftPad = Math.Max(0, (GetContentWidth() - width) / 2);
        var border = new string('─', width - 2);

        WritePadded(leftPad, ConsoleColor.DarkGray, $"┌{border}┐");

        Console.Write(new string(' ', leftPad));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("│ ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("> ");
        Console.ResetColor();
    }

    public static void FinishPrompt()
    {
        Console.WriteLine();
        var width = Math.Clamp(GetContentWidth() - 10, 48, 96);
        var leftPad = Math.Max(0, (GetContentWidth() - width) / 2);
        var border = new string('─', width - 2);
        WritePadded(leftPad, ConsoleColor.DarkGray, $"└{border}┘");
        Console.WriteLine();
    }

    public static void WriteInfo(string message)
    {
        WritePanel("info", message, ConsoleColor.DarkCyan);
    }

    public static void WriteError(string message)
    {
        WritePanel("error", message, ConsoleColor.Red);
    }

    public static void WriteAssistantPrefix(string provider, string model)
    {
        var label = $" assistant [{provider}/{model}] ";
        var width = Math.Clamp(GetContentWidth() - 10, 48, 104);
        var leftPad = Math.Max(0, (GetContentWidth() - width) / 2);
        var border = new string('─', Math.Max(2, width - label.Length - 2));

        Console.Write(new string(' ', leftPad));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("┌");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(label);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(border);
        Console.WriteLine("┐");

        Console.Write(new string(' ', leftPad));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("│ ");
        Console.ResetColor();
    }

    public static void FinishAssistantBlock()
    {
        Console.WriteLine();
        var width = Math.Clamp(GetContentWidth() - 10, 48, 104);
        var leftPad = Math.Max(0, (GetContentWidth() - width) / 2);
        var border = new string('─', width - 2);
        WritePadded(leftPad, ConsoleColor.DarkGray, $"└{border}┘");
        Console.WriteLine();
    }

    private static void WriteTopChrome()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("● ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("● ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("●");
        Console.ResetColor();
    }

    private static void WriteShortcutGrid(int width)
    {
        var commandWidth = 18;
        var descriptionWidth = 20;
        foreach (var item in Shortcuts)
        {
            var line = $"{item.Command.PadRight(commandWidth)} {item.Description.PadRight(descriptionWidth)}";
            WriteCentered(line, ConsoleColor.Gray, width, highlight: item.Command);
        }
    }

    private static void WriteStatusLine(AppConfig config, int width)
    {
        var parts = new[]
        {
            $"mode {config.RunMode}",
            $"provider {config.ActiveProvider}",
            $"model {config.ActiveModel}",
            $"session {config.CurrentSessionName}"
        };

        WriteCentered(string.Join("   ", parts), ConsoleColor.DarkGray, width);
    }

    private static void WritePanel(string title, string message, ConsoleColor accent)
    {
        var lines = SplitLines(message);
        var width = Math.Clamp(GetContentWidth() - 12, 44, 104);
        var leftPad = Math.Max(0, (GetContentWidth() - width) / 2);
        var label = $" {title} ";
        var border = new string('─', Math.Max(2, width - label.Length - 2));

        Console.Write(new string(' ', leftPad));
        Console.ForegroundColor = accent;
        Console.Write("┌");
        Console.Write(label);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(border);
        Console.WriteLine("┐");

        foreach (var line in lines)
        {
            Console.Write(new string(' ', leftPad));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("│ ");
            Console.ResetColor();
            Console.WriteLine(line);
        }

        Console.Write(new string(' ', leftPad));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"└{new string('─', width - 2)}┘");
        Console.ResetColor();
    }

    private static void WriteCentered(string text, ConsoleColor color, int width, string? highlight = null)
    {
        var safe = text ?? string.Empty;
        var leftPad = Math.Max(0, (width - safe.Length) / 2);
        Console.Write(new string(' ', leftPad));

        if (!string.IsNullOrWhiteSpace(highlight) && safe.Contains(highlight, StringComparison.Ordinal))
        {
            var index = safe.IndexOf(highlight, StringComparison.Ordinal);
            var before = safe[..index];
            var match = safe.Substring(index, highlight.Length);
            var after = safe[(index + highlight.Length)..];

            Console.ForegroundColor = color;
            Console.Write(before);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(match);
            Console.ForegroundColor = color;
            Console.WriteLine(after);
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = color;
        Console.WriteLine(safe);
        Console.ResetColor();
    }

    private static void WriteMutedCentered(string text, int width)
    {
        WriteCentered(text, ConsoleColor.DarkGray, width);
    }

    private static void WriteRule(char ch, ConsoleColor color, int width)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(new string(ch, width));
        Console.ResetColor();
    }

    private static void WritePadded(int leftPad, ConsoleColor color, string text)
    {
        Console.Write(new string(' ', leftPad));
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private static int GetContentWidth()
    {
        try
        {
            return Math.Clamp(Console.WindowWidth - 1, 60, 140);
        }
        catch
        {
            return 100;
        }
    }

    private static IReadOnlyList<string> SplitLines(string message)
    {
        return (message ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
    }

    private static string GetVersionText()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}

