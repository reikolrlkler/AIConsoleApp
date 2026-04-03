using AIConsoleApp.Models;

namespace AIConsoleApp.Services;

public static class ModelCatalog
{
    private static readonly IReadOnlyList<ModelDefinition> Models =
    [
        new() { Provider = "openai", ModelId = "gpt-5.1", DisplayName = "GPT-5.1", Aliases = ["gpt5.1", "gpt-5", "gpt5", "chatgpt-5"] },
        new() { Provider = "openai", ModelId = "gpt-5-mini", DisplayName = "GPT-5 mini", Aliases = ["gpt5-mini", "gpt-5 mini"] },
        new() { Provider = "openai", ModelId = "gpt-4.1", DisplayName = "GPT-4.1", Aliases = ["gpt4.1", "gpt-4o", "gpt4o", "gpt-4o-latest"] },
        new() { Provider = "openai", ModelId = "gpt-4.1-mini", DisplayName = "GPT-4.1 mini", Aliases = ["gpt4.1-mini", "gpt-4 mini"] },
        new() { Provider = "openai", ModelId = "gpt-3.5-turbo", DisplayName = "GPT-3.5", Aliases = ["gpt-3.5", "gpt35"] },

        new() { Provider = "anthropic", ModelId = "claude-sonnet-4-20250514", DisplayName = "Claude Sonnet 4", Aliases = ["claude sonnet 4", "claude-4", "sonnet-4", "claude-4-sonnet"] },
        new() { Provider = "anthropic", ModelId = "claude-opus-4-20250514", DisplayName = "Claude Opus 4", Aliases = ["claude opus 4", "opus-4", "claude-4-opus"] },
        new() { Provider = "anthropic", ModelId = "claude-3-7-sonnet-latest", DisplayName = "Claude 3.7 Sonnet", Aliases = ["claude 3.7 sonnet", "claude-3.7-sonnet", "sonnet-3.7"] },
        new() { Provider = "anthropic", ModelId = "claude-3-5-sonnet-latest", DisplayName = "Claude 3.5 Sonnet", Aliases = ["claude 3.5 sonnet", "claude-3.5-sonnet", "sonnet"] },

        new() { Provider = "google", ModelId = "gemini-2.5-pro", DisplayName = "Gemini 2.5 Pro", Aliases = ["gemini 2.5 pro", "gemini-pro", "gemini-pro-latest"] },
        new() { Provider = "google", ModelId = "gemini-2.5-flash", DisplayName = "Gemini 2.5 Flash", Aliases = ["gemini 2.5 flash", "gemini-flash", "gemini-flash-latest"] },
        new() { Provider = "google", ModelId = "gemini-2.5-flash-lite", DisplayName = "Gemini 2.5 Flash-Lite", Aliases = ["gemini 2.5 flash lite", "gemini-flash-lite"] },
        new() { Provider = "google", ModelId = "gemini-3-pro-preview", DisplayName = "Gemini 3 Pro Preview", Aliases = ["gemini 3 pro", "gemini-3-pro"] },
        new() { Provider = "google", ModelId = "gemini-3-flash-preview", DisplayName = "Gemini 3 Flash Preview", Aliases = ["gemini 3 flash", "gemini-3-flash"] },
        new() { Provider = "google", ModelId = "gemini-1.5-pro", DisplayName = "Gemini 1.5 Pro", Aliases = ["gemini 1.5 pro"] },
        new() { Provider = "google", ModelId = "gemini-1.5-flash", DisplayName = "Gemini 1.5 Flash", Aliases = ["gemini 1.5 flash"] },

        new() { Provider = "groq", ModelId = "llama-3.3-70b-versatile", DisplayName = "Llama 3.3 70B", Aliases = ["llama 3.3 70b", "llama-3.3", "grok-2"] },
        new() { Provider = "groq", ModelId = "qwen/qwen3-32b", DisplayName = "Qwen3 32B", Aliases = ["qwen3", "qwen 3 32b"] },
        new() { Provider = "groq", ModelId = "gemma2-9b-it", DisplayName = "Gemma 2 9B", Aliases = ["gemma 2 9b", "gemma2"] },
        new() { Provider = "groq", ModelId = "mixtral-8x7b-32768", DisplayName = "Mixtral 8x7B", Aliases = ["mixtral", "mixtral 8x7b"] },
        new() { Provider = "groq", ModelId = "llama-3.1-8b-instant", DisplayName = "Llama 3.1 8B Instant", Aliases = ["llama 3.1 8b", "llama-3.1-8b"] },

        new() { Provider = "deepseek", ModelId = "deepseek-chat", DisplayName = "DeepSeek Chat / V3", Aliases = ["deepseek-v3", "deepseek v3", "deepseek chat"] },
        new() { Provider = "deepseek", ModelId = "deepseek-reasoner", DisplayName = "DeepSeek Reasoner", Aliases = ["deepseek-r1", "deepseek reasoner", "reasoner"] },
        new() { Provider = "deepseek", ModelId = "deepseek-coder", DisplayName = "DeepSeek Coder", Aliases = ["coder", "deepseek coder"] },

        new() { Provider = "qwen", ModelId = "qwen-max", DisplayName = "Qwen-Max", Aliases = ["qwen max", "qwen-max-latest"] },
        new() { Provider = "qwen", ModelId = "qwen-plus", DisplayName = "Qwen-Plus", Aliases = ["qwen plus", "qwen-plus-latest"] },
        new() { Provider = "qwen", ModelId = "qwen-turbo", DisplayName = "Qwen-Turbo", Aliases = ["qwen turbo"] },

        new() { Provider = "mistral", ModelId = "mistral-large-latest", DisplayName = "Mistral Large", Aliases = ["mistral large", "mistral large 3"] },
        new() { Provider = "mistral", ModelId = "mistral-small-latest", DisplayName = "Mistral Small", Aliases = ["mistral small"] },
        new() { Provider = "mistral", ModelId = "open-mistral-nemo", DisplayName = "Mistral Nemo", Aliases = ["nemo", "mistral nemo"] },

        new() { Provider = "cohere", ModelId = "command-a-03-2025", DisplayName = "Command A", Aliases = ["command-a", "command a"] },
        new() { Provider = "cohere", ModelId = "command-r-08-2024", DisplayName = "Command-R", Aliases = ["command-r", "command r"] },
        new() { Provider = "cohere", ModelId = "command-r-plus-08-2024", DisplayName = "Command-R+", Aliases = ["command-r+", "command r plus"] },
        new() { Provider = "cohere", ModelId = "command", DisplayName = "Command", Aliases = ["command"] },

        new() { Provider = "ollama", ModelId = "llama3.2", DisplayName = "Ollama (local)", RequiresApiKey = false, Aliases = ["ollama"] }
    ];

    public static IReadOnlyList<ModelDefinition> GetAll() => Models;

    public static IReadOnlyList<string> GetProviders()
    {
        return Models
            .Select(static model => model.Provider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ModelDefinition> GetModelsForProvider(string provider)
    {
        provider = NormalizeProvider(provider);
        return Models.Where(model => string.Equals(model.Provider, provider, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static string NormalizeProvider(string? provider)
    {
        var normalized = (provider ?? string.Empty).Trim().ToLowerInvariant();

        return normalized switch
        {
            "grok" => "groq",
            "claude" => "anthropic",
            "gemini" => "google",
            _ => normalized
        };
    }

    public static ModelDefinition ResolveModel(string? provider, string? model)
    {
        var normalizedProvider = NormalizeProvider(provider);
        var normalizedModel = model?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedProvider) && !string.IsNullOrWhiteSpace(normalizedModel))
        {
            var discovered = Models.FirstOrDefault(candidate => candidate.Matches(normalizedModel));
            if (discovered is not null)
            {
                return discovered;
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedProvider) && !string.IsNullOrWhiteSpace(normalizedModel))
        {
            var exact = Models.FirstOrDefault(candidate =>
                string.Equals(candidate.Provider, normalizedProvider, StringComparison.OrdinalIgnoreCase)
                && candidate.Matches(normalizedModel));

            if (exact is not null)
            {
                return exact;
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedProvider))
        {
            return GetDefaultForProvider(normalizedProvider);
        }

        return GetDefaultForProvider("openai");
    }

    public static ModelDefinition GetDefaultForProvider(string provider)
    {
        provider = NormalizeProvider(provider);

        return Models.FirstOrDefault(candidate => string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase))
            ?? new ModelDefinition
            {
                Provider = provider,
                ModelId = provider == "ollama" ? "llama3.2" : "gpt-5.1",
                DisplayName = provider == "ollama" ? "Ollama (local)" : "Custom",
                RequiresApiKey = !string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase)
            };
    }

    public static string InferProviderFromModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return "openai";
        }

        return Models.FirstOrDefault(candidate => candidate.Matches(model))?.Provider ?? "openai";
    }
}
