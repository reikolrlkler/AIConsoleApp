# AIConsoleApp

Alpha-версия интерактивного консольного AI-клиента на C# / .NET 8 в стиле Claude Code.

## Status

Текущая стадия: alpha.

Что уже работает:

- интерактивный чат;
- `/addkey`, `/listkeys`, `/delkey`, `/activekey`;
- `/model`, `/model list`, `/model set`, `/ask`;
- `/history`, `/savechat`, `/loadchat`, `/clear`, `/exit`;
- fallback по нескольким ключам одного провайдера;
- streaming для поддерживаемых API;
- live refresh моделей в `/model list` с кэшем в конфиге;
- таймауты, ретраи и файловое логирование;
- сохранение конфига в `%APPDATA%/AIConsole/config.json`;
- базовые автотесты для core-логики.

## Провайдеры

- OpenAI
- Anthropic
- Google Gemini
- Groq
- DeepSeek
- Qwen
- Mistral
- Cohere
- Ollama

## Запуск

```bash
dotnet restore
dotnet run
```

## Тесты

```bash
dotnet test AIConsoleApp.Tests/AIConsoleApp.Tests.csproj
```

## Быстрый старт

```text
/addkey provider=openai key=sk-...
/model set provider=openai model=gpt-5.1
Привет, кто ты?
/model list
/history 10
```

## Notes

- Каталог встроенных моделей обновлён под более свежие линейки: GPT-5.x, Claude 4, Gemini 2.5/3, новые Cohere и актуальные семейства у Groq.
- `/model list` не ограничивается статическим списком: команда делает live refresh через API провайдера там, где это поддерживается, и сохраняет найденные model id в конфиг.
- Поэтому статический каталог нужен как удобный старт и набор алиасов, а не как единственный источник правды.
- Для `Qwen` можно переопределить endpoint через `AICONSOLE_QWEN_BASE_URL`.
- Для `Ollama` можно переопределить адрес через `AICONSOLE_OLLAMA_BASE_URL`.
- OpenAI, Anthropic и Google идут через официальные SDK; остальные провайдеры используют совместимые REST API.
- Логи пишутся в `%APPDATA%/AIConsole/logs/`.
- Проект собран как alpha: без претензии на production-hardening всех внешних API.
