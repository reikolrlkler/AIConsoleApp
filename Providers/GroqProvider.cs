using AIConsoleApp.Services;

namespace AIConsoleApp.Providers;

public sealed class GroqProvider : OpenAiCompatibleProvider
{
    public GroqProvider(string model, KeyManager keyManager, HttpClient httpClient, ProviderRuntimeOptions runtimeOptions, IAppLogger logger)
        : base("groq", model, keyManager, httpClient, runtimeOptions, logger, "https://api.groq.com/openai/v1")
    {
    }
}
