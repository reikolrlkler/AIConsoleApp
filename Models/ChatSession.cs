namespace AIConsoleApp.Models;

public sealed class ChatSession
{
    public string Name { get; set; } = "default";

    public List<ChatMessage> Messages { get; set; } = new();
}
