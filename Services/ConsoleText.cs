namespace AIConsoleApp.Services;

public sealed class ConsoleText
{
    private ConsoleText(string language)
    {
        Language = AppLanguage.NormalizeOrDefault(language);
    }

    public string Language { get; }

    public bool IsEnglish => string.Equals(Language, AppLanguage.English, StringComparison.OrdinalIgnoreCase);

    public string HelpText => IsEnglish
        ? @"Commands:
  /help
  /language
  /language set en
  /language set ru
  /mode
  /mode set plan
  /mode set build
  /mode set autopilot
  /tools
  /tool action=grep pattern=TODO path=.
  /addkey provider=openai key=sk-...
  /listkeys
  /delkey provider=openai key=sk-...
  /delkey provider=openai all
  /activekey provider=openai key=sk-...
  /model
  /model list
  /model set provider=openai model=gpt-5.1
  /ask provider=groq model=llama-3.3-70b-versatile ""Your question""
  /stream
  /stream on
  /stream off
  /timeout
  /timeout 120
  /retries
  /retries 3
  /clear
  /history
  /history 20
  /savechat chat.json
  /savemd chat.md
  /loadchat chat.json
  /chat list
  /chat new notes
  /chat switch notes
  /chat delete notes
  /ls
  /ls path=.
  /read Program.cs
  /mkdir temp\logs
  /write notes.txt ""hello""
  /doctor
  /exit

Modes:
  plan       planning/chat only, no AI tool execution from prompts
  build      manual commands and slash tools, no AI autopilot execution
  autopilot  allows AI-planned tool execution from normal prompts

Behavior:
  Any line without / is sent to the current active model.
  In autopilot mode, normal prompts may trigger tool actions before falling back to chat.
  /tools lists the built-in toolset.
  /tool runs one tool action directly.
  /model list performs live model refresh where supported and falls back to cached results.
  /doctor checks config, sessions, providers, tools, and local environment basics."
        : @"Команды:
  /help
  /language
  /language set en
  /language set ru
  /mode
  /mode set plan
  /mode set build
  /mode set autopilot
  /tools
  /tool action=grep pattern=TODO path=.
  /addkey provider=openai key=sk-...
  /listkeys
  /delkey provider=openai key=sk-...
  /delkey provider=openai all
  /activekey provider=openai key=sk-...
  /model
  /model list
  /model set provider=openai model=gpt-5.1
  /ask provider=groq model=llama-3.3-70b-versatile ""Твой вопрос""
  /stream
  /stream on
  /stream off
  /timeout
  /timeout 120
  /retries
  /retries 3
  /clear
  /history
  /history 20
  /savechat chat.json
  /savemd chat.md
  /loadchat chat.json
  /chat list
  /chat new notes
  /chat switch notes
  /chat delete notes
  /ls
  /ls path=.
  /read Program.cs
  /mkdir temp\logs
  /write notes.txt ""hello""
  /doctor
  /exit

Режимы:
  plan       только планирование/чат, без AI-выполнения tools из обычных фраз
  build      ручные команды и slash-tools, без AI-autopilot выполнения
  autopilot  разрешает AI-планирование и выполнение tools из обычных фраз

Поведение:
  Любая строка без / отправляется в текущую активную модель.
  В режиме autopilot обычные фразы могут запускать tools до обычного ответа модели.
  /tools показывает встроенный набор инструментов.
  /tool напрямую запускает одно действие инструмента.
  /model list делает live refresh моделей там, где это поддерживается, и использует кэш при недоступности API.
  /doctor проверяет конфиг, сессии, провайдеров, tools и базовое локальное окружение.";

    public string BannerTitle => "AIConsole for .NET 8";
    public string ConfigLabel => IsEnglish ? "Config" : "Конфиг";
    public string LogsLabel => IsEnglish ? "Logs" : "Логи";
    public string ActiveLabel => IsEnglish ? "Active" : "Активно";
    public string BannerFooter => IsEnglish ? "Type /help for commands. Ctrl+C cancels the current request." : "Используй /help для списка команд. Ctrl+C отменяет текущий запрос.";
    public string RequestCanceled => IsEnglish ? "Request canceled." : "Запрос отменён.";
    public string HistoryCleared => IsEnglish ? "Current chat history cleared." : "История текущего диалога очищена.";
    public string HistoryEmpty => IsEnglish ? "History is empty." : "История пуста.";
    public string NoSavedKeys => IsEnglish ? "No saved keys yet." : "Сохранённых ключей пока нет.";
    public string DuplicateOrInvalidKey => IsEnglish ? "The key already exists or the arguments are invalid." : "Ключ уже существует или аргументы некорректны.";
    public string NothingRemoved => IsEnglish ? "Nothing was removed. Check the provider and key." : "Ничего не удалено: проверь провайдера и ключ.";
    public string ActiveKeyNotFound => IsEnglish ? "Failed to activate key: key not found." : "Не удалось активировать ключ: ключ не найден.";
    public string AddKeyUsage => IsEnglish ? "Usage: /addkey provider=openai key=sk-..." : "Использование: /addkey provider=openai key=sk-...";
    public string DeleteKeyUsage => IsEnglish ? "Usage: /delkey provider=openai key=sk-... or /delkey provider=openai all" : "Использование: /delkey provider=openai key=sk-... или /delkey provider=openai all";
    public string ActiveKeyUsage => IsEnglish ? "Usage: /activekey provider=openai key=sk-..." : "Использование: /activekey provider=openai key=sk-...";
    public string AskUsage => IsEnglish ? "Usage: /ask provider=groq model=llama-3.3-70b-versatile \"Your question\"" : "Использование: /ask provider=groq model=llama-3.3-70b-versatile \"Твой вопрос\"";
    public string SaveChatUsage => IsEnglish ? "Usage: /savechat filename.json" : "Использование: /savechat filename.json";
    public string LoadChatUsage => IsEnglish ? "Usage: /loadchat filename.json" : "Использование: /loadchat filename.json";
    public string SaveMarkdownUsage => IsEnglish ? "Usage: /savemd filename.md" : "Использование: /savemd filename.md";
    public string ModelSetUsage => IsEnglish ? "Usage: /model set provider=openai model=gpt-5.1" : "Использование: /model set provider=openai model=gpt-5.1";
    public string ModelCommandUsage => IsEnglish ? "Usage: /model, /model list, or /model set provider=... model=..." : "Использование: /model, /model list или /model set provider=... model=...";
    public string LanguageUsage => IsEnglish ? "Usage: /language or /language set en|ru" : "Использование: /language или /language set en|ru";

    public string CurrentModel(string provider, string model) => IsEnglish ? $"Current model: {provider}/{model}" : $"Текущая модель: {provider}/{model}";
    public string ActiveModelUpdated(string provider, string model) => IsEnglish ? $"Active model: {provider}/{model}" : $"Активная модель: {provider}/{model}";
    public string KeyAdded(string provider) => IsEnglish ? $"Key added for provider '{provider}'." : $"Ключ добавлен для провайдера '{provider}'.";
    public string KeyDeleted => IsEnglish ? "Key deleted." : "Ключ удалён.";
    public string AllKeysDeleted(string provider) => IsEnglish ? $"All keys for provider '{provider}' were removed." : $"Все ключи провайдера '{provider}' удалены.";
    public string ActiveKeyUpdated(string provider) => IsEnglish ? $"Active key for '{provider}' updated." : $"Активный ключ для '{provider}' обновлён.";
    public string TranscriptSaved(string path) => IsEnglish ? $"Chat saved to {path}" : $"Диалог сохранён в {path}";
    public string TranscriptLoaded(string path) => IsEnglish ? $"Chat loaded from {path}" : $"Диалог загружен из {path}";
    public string UnknownCommand(string command) => IsEnglish ? $"Unknown command: {command}. Use /help." : $"Неизвестная команда: {command}. Используй /help.";
    public string CurrentLanguage(string language) => IsEnglish ? $"Current language: {language}" : $"Текущий язык: {language}";
    public string LanguageUpdated(string language) => IsEnglish ? $"Language updated: {language}" : $"Язык обновлён: {language}";
    public string ActiveState(bool isActive) => IsEnglish ? $"active={(isActive ? Yes : No)}" : $"активен={(isActive ? Yes : No)}";
    public string KeyState(string value) => IsEnglish ? $"key={value}" : $"ключ={value}";
    public string Yes => IsEnglish ? "yes" : "да";
    public string No => IsEnglish ? "no" : "нет";
    public string NotApplicable => IsEnglish ? "n/a" : "н/д";
    public string LiveTag => "live";
    public string CachedTag => IsEnglish ? "cached" : "кэш";
    public string NoKnownModels(string keyState) => IsEnglish ? $"    {KeyState(keyState)} no known models" : $"    {KeyState(keyState)} нет известных моделей";
    public string NoProviderKeysMessage(string provider) => IsEnglish ? $"No keys configured for provider '{provider}'. Add one with /addkey provider={provider} key=..." : $"Для провайдера '{provider}' нет ключей. Добавьте ключ командой /addkey provider={provider} key=...";
    public string AllKeysFailedMessage(string provider, string suffix) => IsEnglish ? $"No key for provider '{provider}' worked. Add a new key with /addkey provider={provider} key=...{suffix}" : $"Ни один ключ провайдера '{provider}' не сработал. Добавьте новый ключ: /addkey provider={provider} key=...{suffix}";
    public string RecentErrors(IEnumerable<string> errors) => IsEnglish ? $" Recent errors: {string.Join(" | ", errors)}" : $" Последние ошибки: {string.Join(" | ", errors)}";
    public string NetworkOrTlsError(string provider, string message) => IsEnglish ? $"{provider}: network or TLS error - {message}" : $"{provider}: сеть или TLS ошибка - {message}";
    public string TimeoutMessage(string provider, double seconds, string message) => IsEnglish ? $"{provider}: request timed out after {seconds:0}s - {message}" : $"{provider}: истёк таймаут {seconds:0}s - {message}";

    public string DescribeLanguage(string language)
    {
        return AppLanguage.Normalize(language) switch
        {
            AppLanguage.Russian => IsEnglish ? "Russian" : "русский",
            _ => IsEnglish ? "English" : "английский"
        };
    }

    public static ConsoleText For(string? language)
    {
        return new ConsoleText(language ?? string.Empty);
    }
}
