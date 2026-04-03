namespace AIConsoleApp.Services;

public sealed class ProviderRuntimeOptions
{
    public string Language { get; init; } = AppLanguage.English;

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(90);

    public int MaxRetriesPerKey { get; init; } = 2;
}
