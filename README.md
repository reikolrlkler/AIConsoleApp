# AIConsoleApp

Alpha version of an interactive AI console for C# / .NET 8 with a Claude Code-style workflow.

## Status

Current stage: alpha.

What already works:

- interactive chat;
- key management with rotation and fallback;
- model switching, live model discovery, and per-request overrides;
- shared system prompt for cleaner CLI-style replies;
- first-run language selection with saved preference;
- runtime controls for streaming, timeout, and retries;
- multi-chat sessions with switching and deletion;
- file and workspace commands such as `/pwd`, `/cd`, `/ls`, `/read`, `/mkdir`, and `/write`;
- AI-driven workspace actions for creating folders, creating files, writing text, and changing the working directory from regular prompts;
- JSON and Markdown chat export;
- `/doctor` environment and config diagnostics;
- config persistence in `%APPDATA%/AIConsole/config.json`;
- file logging, self-contained Windows publishing, and basic automated tests.

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

## Windows Build

Framework-dependent publish:

```bash
dotnet publish -c Release -r win-x64 -o publish/win-x64
```

Self-contained publish:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o publish/win-x64-self
```

## Tests

```bash
dotnet test AIConsoleApp.Tests/AIConsoleApp.Tests.csproj
```

## Quick Start

```text
/addkey provider=google key=AIza...
/model set provider=google model=gemini-2.5-flash
/doctor
Hello
```

## Useful Commands

```text
/pwd
/cd notes
/stream off
/timeout 120
/retries 3
/chat new scratch
/chat switch scratch
/ls
/read Program.cs
/mkdir notes
/write notes/todo.txt "ship alpha"
/savechat chat.json
/savemd chat.md
```

## Natural Actions

```text
create folder demo
create file demo\hello.py and write there print('Hello, world!')
go to notes and create file todo.txt with "ship alpha"
create folder temp
create file temp\hello.py and write there print('Hello, world!')
go to notes
```

## Notes

- The built-in model catalog has been refreshed with newer families such as GPT-5.x, Claude 4, Gemini 2.5/3, newer Cohere models, and more recent Groq options.
- `/model list` is not limited to the static catalog: it performs live refresh through provider APIs where supported and stores discovered model IDs in config.
- The static catalog is intended as a good starting point and alias list, not the only source of truth.
- Regular prompts can trigger AI-planned local workspace actions before falling back to a normal chat response.
- OpenAI, Anthropic, and Google use official SDKs. Other providers use compatible REST APIs.
- You can override the Qwen endpoint through `AICONSOLE_QWEN_BASE_URL`.
- You can override the Ollama endpoint through `AICONSOLE_OLLAMA_BASE_URL`.
- Logs are written to `%APPDATA%/AIConsole/logs/`.
- Workspace file commands and local natural-language actions are limited to the current workspace for safety.
- This project is currently alpha and is not positioned as fully production-hardened for every external API.
