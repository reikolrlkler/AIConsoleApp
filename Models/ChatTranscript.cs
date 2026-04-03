namespace AIConsoleApp.Models;

public sealed class ChatTranscript
{
    public string Provider { get; set; } = "openai";

    public string Model { get; set; } = "gpt-5.1";

    public List<ChatMessage> Messages { get; set; } = new();
}
