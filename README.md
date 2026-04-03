# AIConsoleApp

Alpha version of an interactive AI console client for C# / .NET 8 with a Claude Code-style workflow.

## Status

Current stage: alpha.

What already works:

- interactive chat;
- `/addkey`, `/listkeys`, `/delkey`, `/activekey`;
- `/model`, `/model list`, `/model set`, `/ask`;
- `/history`, `/savechat`, `/loadchat`, `/clear`, `/exit`;
- fallback across multiple keys for the same provider;
- streaming for supported APIs;
- live model refresh in `/model list` with config caching;
- timeouts, retries, and file logging;
- config persistence in `%APPDATA%/AIConsole/config.json`;
- basic automated tests for core logic.

## Providers

- OpenAI
- Anthropic
- Google Gemini
- Groq
- DeepSeek
- Qwen
- Mistral
- Cohere
- Ollama

## Run

```bash
dotnet restore
dotnet run
```

## Tests

```bash
dotnet test AIConsoleApp.Tests/AIConsoleApp.Tests.csproj
```

## Quick Start

```text
/addkey provider=openai key=sk-...
/model set provider=openai model=gpt-5.1
Hello, who are you?
/model list
/history 10
```

## Notes

- The built-in model catalog has been refreshed with newer families such as GPT-5.x, Claude 4, Gemini 2.5/3, newer Cohere models, and more recent Groq options.
- `/model list` is not limited to the static catalog: it performs live refresh through provider APIs where supported and stores discovered model IDs in config.
- The static catalog is intended as a good starting point and alias list, not the only source of truth.
- You can override the Qwen endpoint through `AICONSOLE_QWEN_BASE_URL`.
- You can override the Ollama endpoint through `AICONSOLE_OLLAMA_BASE_URL`.
- OpenAI, Anthropic, and Google use official SDKs. Other providers use compatible REST APIs.
- Logs are written to `%APPDATA%/AIConsole/logs/`.
- This project is currently alpha and is not positioned as fully production-hardened for every external API.
