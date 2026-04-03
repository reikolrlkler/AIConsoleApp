namespace AIConsoleApp.Services;

public static class AppLanguage
{
    public const string English = "en";

    public const string Russian = "ru";

    public static string Normalize(string? language)
    {
        var normalized = (language ?? string.Empty).Trim().ToLowerInvariant();

        return normalized switch
        {
            "en" or "eng" or "english" => English,
            "ru" or "rus" or "russian" or "русский" => Russian,
            _ => string.Empty
        };
    }

    public static string NormalizeOrDefault(string? language, string fallback = English)
    {
        var normalized = Normalize(language);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
