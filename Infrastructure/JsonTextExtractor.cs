using System.Text;
using System.Text.Json;

namespace AIConsoleApp.Infrastructure;

public static class JsonTextExtractor
{
    public static string ExtractText(JsonElement element)
    {
        var builder = new StringBuilder();
        AppendText(builder, element);
        return builder.ToString();
    }

    private static void AppendText(StringBuilder builder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                builder.Append(element.GetString());
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AppendText(builder, item);
                }

                break;

            case JsonValueKind.Object:
                if (element.TryGetProperty("text", out var text))
                {
                    AppendText(builder, text);
                }

                if (element.TryGetProperty("content", out var content))
                {
                    AppendText(builder, content);
                }

                if (element.TryGetProperty("parts", out var parts))
                {
                    AppendText(builder, parts);
                }

                if (element.TryGetProperty("message", out var message))
                {
                    AppendText(builder, message);
                }

                if (element.TryGetProperty("delta", out var delta))
                {
                    AppendText(builder, delta);
                }

                break;
        }
    }
}
