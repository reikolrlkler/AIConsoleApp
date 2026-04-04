# AIConsoleApp

Interactive AI console for C# / .NET 8 with a Claude Code-style workflow.

## Status

Current stage: stable.

What is included:

- interactive multi-provider chat;
- API key management with rotation and fallback;
- model switching, live model discovery, and per-request overrides;
- shared system prompt for cleaner CLI-style replies;
- first-run language selection with saved preference;
- three operating modes: `plan`, `build`, and `autopilot`;
- runtime controls for streaming, timeout, and retries;
- multi-chat sessions with switching and deletion;
- file and workspace commands such as `/pwd`, `/cd`, `/ls`, `/read`, `/mkdir`, and `/write`;
- AI-driven workspace actions for creating folders, creating files, writing text, searching files, fetching URLs, running commands, and changing the working directory from regular prompts in `autopilot` mode;
- a built-in tool system with 20+ actions;
- JSON and Markdown chat export;
- `/doctor` environment and config diagnostics;
- persistent config and chat history;
- improved console shell UI inspired by modern coding CLIs;
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

## Modes

- `plan` - planning/chat only, no AI tool execution from regular prompts
- `build` - manual slash commands and direct tool commands, no AI autopilot execution
- `autopilot` - allows AI-planned tool execution from normal prompts

## Tool Actions

Built-in tools include:

- `pwd`, `cd`, `ls`, `glob`, `grep`
- `read_file`, `write_file`, `append_file`
- `mkdir`, `move`, `copy`, `delete_file`, `delete_dir`, `stat`
- `fetch`, `web_search`
- `shell`
- `npm_install`, `npm_uninstall`, `npm_run`
- `pip_install`, `pip_uninstall`
- `dotnet_restore`, `dotnet_build`, `dotnet_test`, `dotnet_publish`

## Quick Start

```text
/addkey provider=google key=AIza...
/model set provider=google model=gemini-2.5-flash
/mode set autopilot
/doctor
Hello
```

## Useful Commands

```text
/mode
/mode set plan
/mode set build
/mode set autopilot
/tools
/tool action=grep pattern=TODO path=.
/tool action=fetch url=https://example.com
/tool action=dotnet_build
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

## Natural Autopilot Examples

```text
search the project for TODO comments
find Program.cs and read it
fetch https://example.com
run dotnet build
create folder demo and write hello.py with print('Hello, world!')
```

## Notes

- The built-in model catalog has been refreshed with newer families such as GPT-5.x, Claude 4, Gemini 2.5/3, newer Cohere models, and more recent Groq options.
- `/model list` is not limited to the static catalog: it performs live refresh through provider APIs where supported and stores discovered model IDs in config.
- The static catalog is intended as a good starting point and alias list, not the only source of truth.
- Regular prompts can trigger AI-planned tool actions only in `autopilot` mode.
- OpenAI, Anthropic, and Google use official SDKs. Other providers use compatible REST APIs.
- You can override the Qwen endpoint through `AICONSOLE_QWEN_BASE_URL`.
- You can override the Ollama endpoint through `AICONSOLE_OLLAMA_BASE_URL`.
- Workspace file tools are limited to the current workspace for safety.
