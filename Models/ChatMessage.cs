namespace AIConsoleApp.Models;

public sealed class ChatMessage
{
    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public ChatMessage Clone()
    {
        return new ChatMessage
        {
            Role = Role,
            Content = Content,
            Timestamp = Timestamp
        };
    }
}
