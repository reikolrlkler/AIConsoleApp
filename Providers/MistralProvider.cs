using AIConsoleApp.Services;

namespace AIConsoleApp.Providers;

public sealed class MistralProvider : OpenAiCompatibleProvider
{
    public MistralProvider(string model, KeyManager keyManager, HttpClient httpClient, ProviderRuntimeOptions runtimeOptions, IAppLogger logger)
        : base("mistral", model, keyManager, httpClient, runtimeOptions, logger, "https://api.mistral.ai/v1")
    {
    }
}
