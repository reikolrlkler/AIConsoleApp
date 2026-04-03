using AIConsoleApp.Models;
using AIConsoleApp.Providers;

namespace AIConsoleApp.Services;

public static class ProviderFactory
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    public static AiProvider Create(string provider, string model, KeyManager keyManager, ProviderRuntimeOptions runtimeOptions, IAppLogger logger)
    {
        provider = ModelCatalog.NormalizeProvider(provider);

        return provider switch
        {
            "openai" => new OpenAiProvider(model, keyManager, SharedHttpClient, runtimeOptions, logger),
            "anthropic" => new AnthropicProvider(model, keyManager, SharedHttpClient, runtimeOptions, logger),
            "google" => new GoogleProvider(model, keyManager, SharedHttpClient, runtimeOptions, logger),
            "groq" => new GroqProvider(model, keyManager, SharedHttpClient, runtimeOptions, logger),
            "deepseek" => new DeepSeekProvider(model, keyManager, SharedHttpClient, runtimeOptions, logger),
            "qwen" => new QwenProvider(model, keyManager, SharedHttpClient, runtimeOptions, logger),
            "mistral" => new MistralProvider(model, keyManager, SharedHttpClient, runtimeOptions, logger),
            "cohere" => new CohereProvider(model, keyManager, SharedHttpClient, runtimeOptions, logger),
            "ollama" => new OllamaProvider(model, keyManager, SharedHttpClient, runtimeOptions, logger),
            _ => throw new InvalidOperationException($"Неизвестный провайдер: {provider}")
        };
    }

    public static HttpClient HttpClient => SharedHttpClient;

    public static ProviderRuntimeOptions CreateRuntimeOptions(AppConfig config)
    {
        config.Normalize();
        return new ProviderRuntimeOptions
        {
            Language = AppLanguage.NormalizeOrDefault(config.Language),
            RequestTimeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds),
            MaxRetriesPerKey = config.MaxRetriesPerKey
        };
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("AIConsoleApp/1.1-alpha");
        return client;
    }
}
