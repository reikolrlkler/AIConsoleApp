using System.Text;

namespace AIConsoleApp.Services;

public static class CommandTokenizer
{
    public static IReadOnlyList<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var buffer = new StringBuilder();
        var inQuotes = false;
        var quoteChar = '\0';

        for (var index = 0; index < input.Length; index++)
        {
            var current = input[index];

            if (current == '\\' && index + 1 < input.Length)
            {
                var next = input[index + 1];
                if (next == '"' || next == '\'' || next == '\\')
                {
                    buffer.Append(next);
                    index++;
                    continue;
                }
            }

            if (current == '"' || current == '\'')
            {
                if (!inQuotes)
                {
                    inQuotes = true;
                    quoteChar = current;
                    continue;
                }

                if (quoteChar == current)
                {
                    inQuotes = false;
                    quoteChar = '\0';
                    continue;
                }
            }

            if (char.IsWhiteSpace(current) && !inQuotes)
            {
                FlushToken(tokens, buffer);
                continue;
            }

            buffer.Append(current);
        }

        if (inQuotes)
        {
            throw new InvalidOperationException("Незакрытая кавычка в команде.");
        }

        FlushToken(tokens, buffer);
        return tokens;
    }

    public static Dictionary<string, string> ParseNamedArguments(IEnumerable<string> tokens)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokens)
        {
            var separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1)
            {
                continue;
            }

            var name = token[..separator].Trim();
            var value = token[(separator + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
            {
                result[name] = value;
            }
        }

        return result;
    }

    private static void FlushToken(List<string> tokens, StringBuilder buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        tokens.Add(buffer.ToString());
        buffer.Clear();
    }
}
