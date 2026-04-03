using AIConsoleApp.Services;

namespace AIConsoleApp.Providers;

public sealed class DeepSeekProvider : OpenAiCompatibleProvider
{
    public DeepSeekProvider(string model, KeyManager keyManager, HttpClient httpClient, ProviderRuntimeOptions runtimeOptions, IAppLogger logger)
        : base("deepseek", model, keyManager, httpClient, runtimeOptions, logger, "https://api.deepseek.com/v1")
    {
    }
}
