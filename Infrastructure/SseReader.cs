using System.Runtime.CompilerServices;
using System.Text;

namespace AIConsoleApp.Infrastructure;

public readonly record struct SseEvent(string Event, string Data);

public static class SseReader
{
    public static async IAsyncEnumerable<SseEvent> ReadEventsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var eventName = string.Empty;
        var dataBuilder = new StringBuilder();

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (dataBuilder.Length > 0)
                {
                    yield return new SseEvent(eventName, dataBuilder.ToString().TrimEnd('\n'));
                    eventName = string.Empty;
                    dataBuilder.Clear();
                }

                continue;
            }

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                eventName = line["event:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                dataBuilder.AppendLine(line["data:".Length..].Trim());
            }
        }

        if (dataBuilder.Length > 0)
        {
            yield return new SseEvent(eventName, dataBuilder.ToString().TrimEnd('\n'));
        }
    }
}
