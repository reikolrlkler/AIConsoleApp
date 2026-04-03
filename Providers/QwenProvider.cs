using AIConsoleApp.Services;

namespace AIConsoleApp.Providers;

public sealed class QwenProvider : OpenAiCompatibleProvider
{
    public QwenProvider(string model, KeyManager keyManager, HttpClient httpClient, ProviderRuntimeOptions runtimeOptions, IAppLogger logger)
        : base(
            "qwen",
            model,
            keyManager,
            httpClient,
            runtimeOptions,
            logger,
            Environment.GetEnvironmentVariable("AICONSOLE_QWEN_BASE_URL")
                ?? "https://dashscope-intl.aliyuncs.com/compatible-mode/v1")
    {
    }
}
