using System.Text.RegularExpressions;

namespace AIConsoleApp.Services;

public static partial class LocalActionInterpreter
{
    public static async Task<string?> TryExecuteAsync(
        string input,
        string currentDirectory,
        Func<string, string> resolvePath,
        Action<string> updateCurrentDirectory,
        CancellationToken ct = default)
    {
        if (TryExtractDirectoryChange(input, out var directoryPath))
        {
            var resolvedDirectory = resolvePath(directoryPath);
            Directory.CreateDirectory(resolvedDirectory);
            updateCurrentDirectory(resolvedDirectory);

            if (TryExtractCreateFolder(input, out var folderPath))
            {
                var folderFullPath = ResolveAgainstCurrent(resolvePath, resolvedDirectory, folderPath);
                Directory.CreateDirectory(folderFullPath);

                if (TryExtractCreateFile(input, out var filePath))
                {
                    var fileFullPath = ResolveAgainstCurrent(resolvePath, resolvedDirectory, filePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fileFullPath)!);
                    var content = TryExtractWriteContent(input) ?? InferTemplateContent(input);
                    await File.WriteAllTextAsync(fileFullPath, content, ct).ConfigureAwait(false);
                    return $"Changed directory to {resolvedDirectory}, created folder {folderFullPath}, and wrote file {fileFullPath}.";
                }

                return $"Changed directory to {resolvedDirectory} and created folder {folderFullPath}.";
            }

            return $"Changed directory to {resolvedDirectory}.";
        }

        if (TryExtractCreateFolder(input, out var singleFolder))
        {
            var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, singleFolder);
            Directory.CreateDirectory(fullPath);
            return $"Created folder: {fullPath}";
        }

        if (TryExtractCreateFile(input, out var singleFile))
        {
            var fullPath = ResolveAgainstCurrent(resolvePath, currentDirectory, singleFile);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var content = TryExtractWriteContent(input) ?? InferTemplateContent(input);
            await File.WriteAllTextAsync(fullPath, content, ct).ConfigureAwait(false);
            return $"Created file: {fullPath}";
        }

        return null;
    }

    private static string ResolveAgainstCurrent(Func<string, string> resolvePath, string currentDirectory, string path)
    {
        var combined = Path.IsPathRooted(path) ? path : Path.Combine(currentDirectory, path);
        return resolvePath(combined);
    }

    private static string? TryExtractWriteContent(string input)
    {
        var match = ContentRegex().Match(input);
        return match.Success ? Cleanup(match.Groups[1].Value) : null;
    }

    private static string InferTemplateContent(string input)
    {
        var lower = input.ToLowerInvariant();
        if (lower.Contains("python") || lower.Contains("пайтон") || lower.Contains("питон"))
        {
            return "print('Hello, world!')";
        }

        return string.Empty;
    }

    private static bool TryExtractDirectoryChange(string input, out string path)
    {
        var match = ChangeDirectoryRegex().Match(input);
        path = match.Success ? Cleanup(match.Groups[1].Value) : string.Empty;
        return !string.IsNullOrWhiteSpace(path);
    }

    private static bool TryExtractCreateFolder(string input, out string path)
    {
        var match = CreateFolderRegex().Match(input);
        path = match.Success ? Cleanup(match.Groups[1].Value) : string.Empty;
        return !string.IsNullOrWhiteSpace(path);
    }

    private static bool TryExtractCreateFile(string input, out string path)
    {
        var match = CreateFileRegex().Match(input);
        path = match.Success ? Cleanup(match.Groups[1].Value) : string.Empty;
        return !string.IsNullOrWhiteSpace(path);
    }

    private static string Cleanup(string value)
    {
        return value.Trim().Trim('"', '\'', '.', ',', ';');
    }

    [GeneratedRegex(@"(?:впиши\s+туда|запиши\s+туда|write\s+(?:there|into\s+it)|put\s+into\s+it)\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ContentRegex();

    [GeneratedRegex(@"(?:зайди\s+(?:сюда|в)?|перейди\s+в|go\s+to|change\s+directory\s+to)\s+(.+?)(?:\s+и\s+создай|\s+and\s+create|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChangeDirectoryRegex();

    [GeneratedRegex(@"(?:создай|сделай|create|make)\s+(?:папк(?:у|а|и)|folder|directory)\s+(.+?)(?:\s+и\s+(?:создай|сделай|впиши|запиши)|\s+and\s+(?:create|write|put)|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CreateFolderRegex();

    [GeneratedRegex(@"(?:создай|сделай|create|make)\s+(?:текстов(?:ый|ой)\s+документ|файл|text\s+file|file)\s+(.+?)(?:\s+и\s+(?:впиши|запиши)|\s+and\s+(?:write|put)|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CreateFileRegex();
}
