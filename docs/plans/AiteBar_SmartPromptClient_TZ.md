# ExecPlan: Smart Prompt Client как встроенная AI-утилита AiteBar

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Этот документ является самодостаточным техническим заданием для Codex/GPT-агента. Агент должен реализовать фичу в текущем репозитории AiteBar без обращения к истории чата. Если в репозитории есть `PLANS.md`, выполнять работу строго по нему: ExecPlan должен оставаться живым документом, фиксировать решения, открытия, прогресс и результат.

## Purpose / Big Picture

Нужно встроить в AiteBar новую утилиту `Smart Prompt Client`. Это не отдельное приложение и не обычный чат. Это встроенный AI-инструмент AiteBar, который открывается с панели у края экрана и позволяет пользователю работать с LLM через собственные API-ключи, собственные карточки промптов и импортируемые платные AI-пакеты.

После реализации пользователь сможет открыть AiteBar, нажать кнопку Smart Prompt Client, добавить API-ключи LLM-провайдеров, импортировать купленный `.aitepkg` пакет с готовыми AI-инструментами, выбрать карточку, вставить текст в одно поле, отправить запрос и получить ответ.

Ключевая продуктовая идея: пользователь может создавать промпты сам, но владелец продукта может продавать готовые файлы AI-пакетов, которые загружаются в утилиту и добавляют профессиональные наборы карточек. AiteBar становится не просто панелью быстрого доступа, а загрузчиком локальных AI-наборов.

## Non-Negotiable Product Requirements

Smart Prompt Client должен быть частью AiteBar, а не внешним процессом или отдельным продуктом.

Утилита должна использовать текущую архитектуру AiteBar: WPF, .NET 10, локальные JSON-файлы в `%APPDATA%`, встроенную систему `UtilityRegistry`, локализацию через `Resources/Strings*.resx`, логирование через `Logger`, настройки через `AppSettings` и общий визуальный стиль AiteBar.

Пользовательские промпты создаются самим пользователем. Приложение не обязано иметь предустановленную библиотеку карточек.

Платные наборы промптов поставляются владельцем продукта как импортируемые файлы. Базовый рекомендуемый формат — `.aitepkg`. Для совместимости разрешить также `.aitepromptpack`, но в интерфейсе и документации использовать основной термин `AI Package` / `AI Toolkit Package`.

API-ключи добавляет сам пользователь. Приложение не должно автоматически создавать аккаунты, обходить капчи, регистрировать email, покупать тарифы или скрыто нарушать правила провайдеров.

Ротация ключей должна быть реализована как failover между пользовательскими ключами для устойчивости и удобства. Если все ключи исчерпали лимиты, приложение должно явно сообщить об этом и остановить повторные попытки.

Сырые API-ключи нельзя писать в логи и нельзя хранить в открытом виде.

## Current AiteBar Context

AiteBar — desktop-утилита для Windows. Она создает скрываемую панель быстрого доступа у края экрана. Панель появляется при наведении курсора или по горячей клавише и содержит пользовательские кнопки, встроенные утилиты, контексты, tray-интеграцию и системные хоткеи.

Текущий стек:

- `.NET 10`
- `WPF`
- `net10.0-windows`
- Windows Forms `NotifyIcon` для tray
- Win32 interop через `NativeMethods.cs`
- JSON-хранение через `System.Text.Json`
- ZIP-пакеты через `System.IO.Compression`
- тесты на `xUnit`

В репозитории есть два основных проекта:

- `AiteBar/` — основное приложение.
- `AiteBar.Tests/` — тесты.

Ключевые существующие файлы:

- `AiteBar/Models.cs` — основные модели приложения, включая `AppSettings` и `CustomElement`.
- `AiteBar/MainWindow.xaml` — основная панель.
- `AiteBar/MainWindow.xaml.cs` и partial-файлы — логика панели, кнопок, tray, хоткеев и запуска утилит.
- `AiteBar/AppSettingsWindow.xaml` и `.cs` — общие настройки приложения.
- `AiteBar/ActionService.cs` — выполнение пользовательских действий и запуск встроенных утилит.
- `AiteBar/UtilityRegistry.cs` — регистрация и запуск встроенных утилит.
- `AiteBar/PathHelper.cs` — пути к данным приложения.
- `AiteBar/Logger.cs` — локальное логирование.
- `AiteBar/Resources/Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, `Strings.de.resx` — локализация.
- `AiteBar/PanelPackageService.cs` — пример существующего импорта/экспорта ZIP-пакетов `.aitebarpanel`.

AiteBar уже имеет встроенные утилиты через `UtilityRegistry`. Новая утилита должна добавляться по той же схеме: отдельное окно, отдельный класс utility, регистрация через атрибут `[Utility]`, запуск через `ActionService.LaunchUtilityAsync()`.

## Product Scope

Smart Prompt Client должен состоять из следующих частей:

1. Окно Smart Prompt Client.
2. Локальное хранилище AI-настроек.
3. Менеджер API-ключей.
4. Автоопределение провайдера по ключу.
5. Импорт моделей от провайдера.
6. Редактор пользовательских карточек промптов.
7. Импорт AI-пакетов `.aitepkg` / `.aitepromptpack`.
8. Одно поле ввода для ежедневной работы.
9. Выполнение запроса к выбранной модели.
10. Failover между доступными пользовательскими ключами.
11. Локализация.
12. Unit-тесты для всей non-UI логики.

## Terminology

`Smart Prompt Client` — встроенная утилита AiteBar для работы с LLM через карточки промптов.

`API Key` — секретный ключ доступа к LLM-провайдеру, который пользователь вставляет вручную.

`Provider` — LLM-сервис, например Groq, Google AI Studio, GitHub Models или OpenRouter.

`Model` — конкретная модель внутри провайдера, например `llama-3.3-70b-versatile`.

`Prompt Card` — карточка задачи. Она содержит название, системный промпт, модель, категорию и параметры генерации.

`AI Package` — импортируемый файл с набором карточек. Основное расширение: `.aitepkg`.

`Prompt Pack` — старое или техническое название пакета промптов. В коде можно использовать `SmartPromptPack`, но в UI лучше писать `AI Package`.

`Failover` — автоматический переход на следующий подходящий ключ, если текущий ключ временно недоступен или получил rate limit.

`Rate limit` — ограничение провайдера на количество запросов или токенов.

## User Journey

### First Launch

Пользователь открывает AiteBar и нажимает кнопку Smart Prompt Client на панели.

Если API-ключей нет, окно показывает пустое состояние:

    API keys are not added yet.
    Add your first API key to use Smart Prompt Client.

Если карточек нет, окно показывает отдельное пустое состояние:

    No prompt cards yet.
    Create a card manually or import an AI Package.

Пользователь может:

- добавить API-ключ;
- создать карточку вручную;
- импортировать `.aitepkg` пакет.

### Adding API Keys

Пользователь вставляет API-ключ в поле. Утилита пытается определить провайдера по регулярному выражению. Если провайдер найден, показать его имя и Base URL. Если не найден, предложить выбрать провайдера вручную.

После нажатия `Add key` утилита:

1. Валидирует ключ.
2. Импортирует список моделей, если провайдер поддерживает endpoint списка моделей.
3. Сохраняет ключ в зашифрованном виде.
4. Показывает статус: Active, Invalid, Rate limited, Unknown.

Пользователь может добавить несколько ключей одного или разных провайдеров.

### Creating Prompt Cards Manually

Пользователь нажимает `New Card` и заполняет:

- name;
- category;
- description;
- system prompt;
- provider;
- model;
- temperature;
- max output tokens;
- icon glyph;
- icon color.

Карточка сохраняется локально и появляется в списке.

### Importing Paid AI Packages

Пользователь получает от владельца продукта файл, например:

    AI Developer Toolkit.aitepkg

Пользователь нажимает `Import Package`, выбирает файл, видит preview:

    AI Developer Toolkit
    Version: 1.0.0
    Author: Codebdbd
    Cards: 152 valid, 0 invalid
    Categories: 12

После подтверждения карточки добавляются в библиотеку.

Важно: пакет не должен заменять пользовательские карточки. Пользовательские карточки всегда сохраняются.

### Daily Use

Пользователь выбирает карточку слева, вставляет текст в одно центральное поле и нажимает `Send` или `Ctrl+Enter`.

Утилита:

1. Берет системный промпт выбранной карточки.
2. Добавляет пользовательский ввод.
3. Выбирает подходящий ключ.
4. Отправляет запрос.
5. Получает ответ.
6. Показывает ответ в правой или нижней области.
7. Позволяет скопировать ответ одной кнопкой.

## Recommended UX Structure

Окно Smart Prompt Client должно быть самостоятельным и не должно перегружать существующий `SettingsWindow` AiteBar.

Минимальная структура окна:

    ┌─────────────────────────────────────────────────────────────┐
    │ Smart Prompt Client                          [Keys] [Packs] │
    ├───────────────────┬─────────────────────────────────────────┤
    │ Search cards      │ Selected Card Title                     │
    │                   │ Provider / Model                        │
    │ Categories        │                                         │
    │                   │ Input                                   │
    │ Card list         │ ┌─────────────────────────────────────┐ │
    │                   │ │ Paste your text here...             │ │
    │ + New Card        │ └─────────────────────────────────────┘ │
    │ Import Package    │ [Send]                                 │
    │                   │                                         │
    │                   │ Response                                │
    │                   │ ┌─────────────────────────────────────┐ │
    │                   │ │ Model output...                     │ │
    │                   │ └─────────────────────────────────────┘ │
    │                   │ [Copy]                                  │
    └───────────────────┴─────────────────────────────────────────┘

UI must follow AiteBar dark style:

- background near `#1A1A1C`;
- panels near `#252526`;
- accent `#007ACC`;
- compact controls;
- no large vertical scrolling for the whole window;
- local scrolling inside card list, input and response areas is acceptable;
- `CornerRadius` 4 for inputs/buttons and 6-8 for panels.

## File and Folder Structure

Create a new folder:

    AiteBar/SmartPrompt/

Create these files:

    AiteBar/SmartPrompt/SmartPromptWindow.xaml
    AiteBar/SmartPrompt/SmartPromptWindow.xaml.cs
    AiteBar/SmartPrompt/SmartPromptUtility.cs
    AiteBar/SmartPrompt/SmartPromptModels.cs
    AiteBar/SmartPrompt/SmartPromptStorageService.cs
    AiteBar/SmartPrompt/SmartPromptSecretProtector.cs
    AiteBar/SmartPrompt/SmartPromptProviderDetector.cs
    AiteBar/SmartPrompt/SmartPromptProviderClient.cs
    AiteBar/SmartPrompt/SmartPromptOpenAiCompatibleClient.cs
    AiteBar/SmartPrompt/SmartPromptGoogleClient.cs
    AiteBar/SmartPrompt/SmartPromptRequestService.cs
    AiteBar/SmartPrompt/SmartPromptPackService.cs
    AiteBar/SmartPrompt/SmartPromptKeyPoolService.cs
    AiteBar/SmartPrompt/SmartPromptModelClassifier.cs
    AiteBar/SmartPrompt/SmartPromptJsonContext.cs

Create tests:

    AiteBar.Tests/SmartPromptProviderDetectorTests.cs
    AiteBar.Tests/SmartPromptPackServiceTests.cs
    AiteBar.Tests/SmartPromptKeyPoolServiceTests.cs
    AiteBar.Tests/SmartPromptStorageServiceTests.cs
    AiteBar.Tests/SmartPromptModelClassifierTests.cs

## PathHelper Changes

Update `AiteBar/PathHelper.cs`.

Add paths:

    public static string SmartPromptDirectory => Path.Combine(AppDataDirectory, "SmartPrompt");
    public static string SmartPromptSettingsFile => Path.Combine(SmartPromptDirectory, "smart_prompt.json");
    public static string SmartPromptPackagesDirectory => Path.Combine(SmartPromptDirectory, "Packages");

Ensure directories are created when AiteBar initializes paths:

    Directory.CreateDirectory(SmartPromptDirectory);
    Directory.CreateDirectory(SmartPromptPackagesDirectory);

Expected physical paths:

    %APPDATA%\Codebdbd\Aite Bar\SmartPrompt\
    %APPDATA%\Codebdbd\Aite Bar\SmartPrompt\smart_prompt.json
    %APPDATA%\Codebdbd\Aite Bar\SmartPrompt\Packages\

## Smart Prompt Data Models

Create `AiteBar/SmartPrompt/SmartPromptModels.cs`.

Required models:

    public sealed class SmartPromptState
    {
        public List<SmartPromptApiKeyProfile> Keys { get; set; } = new();
        public List<SmartPromptCard> PromptCards { get; set; } = new();
        public List<SmartPromptImportedPackage> ImportedPackages { get; set; } = new();
        public string? ActiveCardId { get; set; }
        public string? LastSelectedCategory { get; set; }
    }

    public sealed class SmartPromptApiKeyProfile
    {
        public string KeyId { get; set; } = Guid.NewGuid().ToString("N");
        public string Provider { get; set; } = "";
        public string EncryptedApiKey { get; set; } = "";
        public string MaskedApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string Status { get; set; } = "unknown";
        public DateTimeOffset? CooldownUntil { get; set; }
        public DateTimeOffset? LastUsedAt { get; set; }
        public List<SmartPromptModelInfo> Models { get; set; } = new();
    }

    public sealed class SmartPromptModelInfo
    {
        public string Id { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public string? DisplayName { get; set; }
        public string? ModelClass { get; set; }
        public bool IsCustom { get; set; }
    }

    public sealed class SmartPromptCard
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string SystemPrompt { get; set; } = "";
        public string Provider { get; set; } = "";
        public string ModelId { get; set; } = "";
        public double Temperature { get; set; } = 0.7;
        public int? MaxOutputTokens { get; set; }
        public string IconGlyph { get; set; } = "\uE8D4";
        public string IconColor { get; set; } = "#007ACC";
        public string? SourcePackageId { get; set; }
        public string? SourcePackageVersion { get; set; }
        public bool IsUserEditable { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public sealed class SmartPromptImportedPackage
    {
        public string PackageId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Author { get; set; } = "";
        public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public sealed class SmartPromptCompletionRequest
    {
        public string Provider { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string ModelId { get; set; } = "";
        public string SystemPrompt { get; set; } = "";
        public string UserInput { get; set; } = "";
        public double Temperature { get; set; }
        public int? MaxOutputTokens { get; set; }
    }

    public sealed class SmartPromptCompletionResult
    {
        public bool Success { get; set; }
        public string Text { get; set; } = "";
        public int? HttpStatusCode { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsRateLimited { get; set; }
        public bool IsAuthenticationError { get; set; }
    }

    public sealed class SmartPromptKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public IReadOnlyList<SmartPromptModelInfo> Models { get; set; } = Array.Empty<SmartPromptModelInfo>();
    }

Use string statuses for simplicity and JSON stability:

- `unknown`
- `active`
- `rate_limited`
- `invalid`

## Secret Protection

Create `SmartPromptSecretProtector.cs`.

Use Windows DPAPI with `System.Security.Cryptography.ProtectedData` and `DataProtectionScope.CurrentUser`.

Required API:

    public static class SmartPromptSecretProtector
    {
        public static string Protect(string plainText);
        public static string Unprotect(string protectedText);
    }

Rules:

- Never store raw API keys in `smart_prompt.json`.
- Never log raw API keys.
- When displaying keys, show only masked form.

Masking format:

    gsk_abcd...wxyz

If key is shorter than 12 characters, show:

    ****

## Storage Service

Create `SmartPromptStorageService.cs`.

Responsibilities:

- load `smart_prompt.json`;
- save `smart_prompt.json`;
- create directory if missing;
- recover safely from corrupted JSON;
- avoid data loss by writing through a temporary file and replacing the target file;
- preserve current in-memory state if save fails.

Required behavior:

If `smart_prompt.json` does not exist, return empty `SmartPromptState`.

If JSON is corrupted, rename it to:

    smart_prompt.corrupt.<yyyyMMdd_HHmmss>.json

Then return empty state and log the recovery.

Use `System.Text.Json` with indented output.

Do not throw unhandled exceptions into UI. Return recoverable result or log and show a short message.

## JSON Source Generation

If the project already uses `System.Text.Json` source generation, follow the existing style. If not, normal serializer options are acceptable.

If adding source generation, create `SmartPromptJsonContext.cs`:

    [JsonSerializable(typeof(SmartPromptState))]
    [JsonSerializable(typeof(SmartPromptPackageManifest))]
    internal partial class SmartPromptJsonContext : JsonSerializerContext

## Provider Detection

Create `SmartPromptProviderDetector.cs`.

Required patterns:

Groq:

    Regex: ^gsk_[a-zA-Z0-9]{48,}$
    Provider: groq
    BaseUrl: https://api.groq.com/openai/v1

Google AI Studio:

    Regex: ^AIzaSy[a-zA-Z0-9_-]{33}$
    Provider: google
    BaseUrl: https://generativelanguage.googleapis.com/v1beta

GitHub Models:

    Regex: ^github_pat_[a-zA-Z0-9_]{22,100}$
    Provider: github
    BaseUrl: https://models.inference.ai.azure.com

OpenRouter:

    Regex: ^sk-or-v1-[a-f0-9]{64}$
    Provider: openrouter
    BaseUrl: https://openrouter.ai/api/v1

Required API:

    public sealed record SmartPromptProviderDetectionResult(
        bool IsDetected,
        string Provider,
        string BaseUrl,
        string DisplayName);

    public static class SmartPromptProviderDetector
    {
        public static SmartPromptProviderDetectionResult Detect(string apiKey);
    }

If detection fails, return `IsDetected = false` and empty provider/base URL. UI must allow manual selection.

## Provider API Layer

Create `SmartPromptProviderClient.cs`.

Define:

    public interface ISmartPromptProviderClient
    {
        string Provider { get; }
        Task<SmartPromptKeyValidationResult> ValidateKeyAsync(string apiKey, CancellationToken cancellationToken);
        Task<IReadOnlyList<SmartPromptModelInfo>> ListModelsAsync(string apiKey, CancellationToken cancellationToken);
        Task<SmartPromptCompletionResult> CompleteAsync(SmartPromptCompletionRequest request, CancellationToken cancellationToken);
    }

Implement OpenAI-compatible client:

    SmartPromptOpenAiCompatibleClient

Use it for:

- Groq
- OpenRouter
- GitHub Models if endpoint compatibility works in current codebase/testing environment

Implement Google client separately:

    SmartPromptGoogleClient

The first implementation is text-only. Do not implement file upload, images, audio, or multimodal input.

Provider clients must use `HttpClient`. If the repository already has a shared HTTP pattern, follow it. Otherwise use a private static `HttpClient` per provider client or inject an `HttpClient` where tests need it.

All provider clients must:

- support cancellation;
- handle non-200 responses;
- identify 429 as rate limit;
- identify authentication errors;
- avoid logging raw keys;
- return `SmartPromptCompletionResult` instead of throwing for normal API errors.

## Request Assembly

Create `SmartPromptRequestService.cs`.

When user sends a request:

1. Resolve selected `SmartPromptCard`.
2. Ensure user input is not empty.
3. Ensure card has model ID.
4. Resolve provider. If card provider is empty, find an enabled matching model across providers.
5. Ask `SmartPromptKeyPoolService` for eligible keys.
6. Build request:

       system = card.SystemPrompt
       user = user input field
       model = card.ModelId
       temperature = card.Temperature
       maxOutputTokens = card.MaxOutputTokens

7. Send through provider client.
8. On success, update key `LastUsedAt` and show output.
9. On rate limit, mark key as rate-limited and try next eligible key.
10. Stop after one attempt per eligible key.

No request history is required in first version. Do not save user input and model output by default.

## Key Pool Service

Create `SmartPromptKeyPoolService.cs`.

Responsibilities:

- select eligible key for provider/model;
- skip invalid keys;
- skip keys with non-expired cooldown;
- treat expired cooldown as usable again;
- sort by `LastUsedAt` ascending;
- update status after request;
- prevent infinite retry.

Eligibility rules:

A key is eligible when:

- `Provider` matches requested provider;
- `Status` is `active` or `unknown` with no cooldown;
- `CooldownUntil` is null or earlier than current time;
- model list contains an enabled model matching `modelId`, or model list is empty and custom model is allowed.

On HTTP 200:

- set status `active`;
- set `LastUsedAt = now`;
- clear `CooldownUntil`.

On HTTP 429:

- set status `rate_limited`;
- set cooldown.

Cooldown rules:

- default rate-limit cooldown: 60 seconds;
- daily quota style error: 24 hours if response indicates daily quota exhaustion;
- otherwise do not guess long cooldowns.

On authentication failure:

- set status `invalid`;
- do not retry that key.

If all keys fail:

Show:

    Limits are currently exhausted for this model on all available keys. Try another model or wait until the limit resets.

Compliance rule:

Do not hide full exhaustion from user. Do not implement behavior that creates or rotates accounts automatically.

## AI Package Format

Use `.aitepkg` as the main extension for paid packages.

Also accept `.aitepromptpack` as alias for backward compatibility or internal builds.

A `.aitepkg` file is a ZIP archive with this structure:

    AI Developer Toolkit.aitepkg
        manifest.json
        icons/
            code-review.png
            sql.png
        preview.png
        docs/
            README.md

Only `manifest.json` is required. Other files are optional for future expansion.

The first implementation must support glyph/color icons from manifest. File icons may be ignored initially, but the ZIP structure must validate paths safely so file assets can be implemented later.

## AI Package Manifest

Create manifest model in `SmartPromptPackService.cs` or separate `SmartPromptPackageManifest.cs`.

Manifest example:

    {
      "formatVersion": 1,
      "packageId": "developer-toolkit",
      "type": "ai-toolkit",
      "name": "AI Developer Toolkit",
      "version": "1.0.0",
      "author": "Codebdbd",
      "description": "Professional prompt cards for code review, refactoring, tests and architecture.",
      "minAppVersion": "1.0.0",
      "cards": [
        {
          "name": "Code Review",
          "category": "Development",
          "description": "Find bugs, risks and improvements in pasted code.",
          "systemPrompt": "You are a senior software engineer. Review the provided code...",
          "provider": "groq",
          "modelId": "llama-3.3-70b-versatile",
          "temperature": 0.2,
          "maxOutputTokens": 4096,
          "iconGlyph": "\uE943",
          "iconColor": "#007ACC"
        }
      ]
    }

Required manifest fields:

- `formatVersion`
- `packageId`
- `name`
- `version`
- `cards`

Required card fields:

- `name`
- `systemPrompt`
- `modelId`

Optional card fields:

- `category`
- `description`
- `provider`
- `temperature`
- `maxOutputTokens`
- `iconGlyph`
- `iconColor`

Validation rules:

- `formatVersion` must be 1.
- `packageId` cannot be empty.
- `name` cannot be empty.
- `version` cannot be empty.
- `cards` cannot be null.
- Invalid cards are skipped, not fatal.
- Invalid manifest is fatal for import but must not crash the app.
- ZIP entries must not allow path traversal. Reject entries containing `..`, rooted paths, drive letters, or invalid separators.
- Package import must not execute any code.
- Package import must not run scripts.
- Package import only reads data.

## Package Import UX

Button label:

    Import Package

File dialog filter:

    AiteBar AI Packages (*.aitepkg;*.aitepromptpack)|*.aitepkg;*.aitepromptpack

After selecting file, show preview:

    AI Developer Toolkit
    Author: Codebdbd
    Version: 1.0.0
    Description: Professional prompt cards for developers.
    Valid cards: 152
    Invalid cards: 0
    Categories: 12

Actions:

- `Import`
- `Cancel`

After import:

    Package imported: AI Developer Toolkit. 152 cards added.

If invalid cards were skipped:

    Package imported with warnings. 148 cards added, 4 cards skipped.

## Package Update Rules

If package is new:

- Add valid cards.
- Store `SourcePackageId` and `SourcePackageVersion` on imported cards.
- Add entry to `ImportedPackages`.

If same `packageId` and same `version` already exists:

- Ask whether to reimport.
- Reimport should update existing package cards by `SourcePackageId + Name`.
- Do not duplicate cards with same source package and name.

If same `packageId` and newer version:

- Treat as package update.
- Update matching package cards.
- Add new package cards.
- Do not delete user-created cards.
- Do not delete user-edited cards unless the app clearly tracks them as package-owned and unmodified.

First version simplification:

- Imported package cards can be edited by the user, but editing sets `SourcePackageId = null` and turns the card into a user card. This prevents package update from overwriting user edits.

## Prompt Card Library Structure

In the sidebar, group cards as:

    My Cards
        User-created cards

    AI Developer Toolkit
        Package cards

    Marketing Toolkit
        Package cards

Alternative category view is allowed if simpler:

    All
    Development
    Marketing
    Legal
    My Cards
    Imported Packages

Required behavior:

- Search by card name and description.
- Filter by category.
- Show source package if card came from package.
- Allow duplicate user card creation from package card.

## Model Management

For each key, show available models.

User can:

- enable/disable model with checkbox;
- manually add custom model ID;
- delete custom model;
- refresh model list from provider.

Model hints:

- `8b`, `flash-lite`, `mini` → `light`
- `70b`, `pro`, `gpt-4`, `sonnet` → `smart`
- `flash`, `gemini` → `long-context`
- `r1`, `reason`, `deepseek` → `reasoning`

Create `SmartPromptModelClassifier.cs`.

Hints are only UI labels. They must not block or force model choice.

Suggested UI labels:

- Light / Fast — simple rewrite, correction, translation.
- Smart / Heavy — complex writing, code, reasoning-heavy tasks.
- Long Context — large pasted text, logs, books.
- Reasoning / Logic — math, algorithms, deep analysis.

## AiteBar Utility Integration

Create `AiteBar/SmartPrompt/SmartPromptUtility.cs`:

    using System.Runtime.Versioning;
    using System.Windows;

    namespace AiteBar;

    [SupportedOSPlatform("windows6.1")]
    [Utility]
    public sealed class SmartPromptUtility : UtilityBase<SmartPromptWindow>
    {
        public override string Id => "SmartPrompt";
        public override string DisplayNameKey => "Tool_SmartPrompt";
        public override string IconGlyph => "\uE8D4";
        public override string IconColor => "#007ACC";

        protected override SmartPromptWindow CreateWindow(AppSettingsService settingsService, Window? owner)
        {
            return new SmartPromptWindow(settingsService) { Owner = owner };
        }

        protected override void ShowWindow(SmartPromptWindow window, AppSettingsService settingsService)
        {
            window.Show();
            window.Activate();
        }
    }

Update `AiteBar/MainWindow.xaml`:

- Add Smart Prompt button to the system utility panel.
- Keep the panel compact.
- Do not break existing system utility buttons.

Update `AiteBar/MainWindow.xaml.cs`:

- Add localized tooltip in `ApplyLocalizedText()`.
- Add context menu entry in `AttachSystemUtilityContextMenus()`.
- Update visible system button count logic.
- Update system utility visibility logic.
- Add click handler:

      private async void BtnSmartPrompt_Click(object sender, RoutedEventArgs e)
      {
          await RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("SmartPrompt", HideDock));
      }

If the project uses `UnifiedButtonService` for system buttons, update it too.

Update `AiteBar/Models.cs`:

    public bool ShowPresetSmartPrompt { get; set; } = true;

Update `AiteBar/AppSettingsWindow.xaml` and `.cs`:

- Add checkbox for showing/hiding Smart Prompt button.
- Load value from settings.
- Save value to settings.

If adding a global hotkey for Smart Prompt in first version, update:

- `Models.cs`
- `HotkeyService.cs`
- `AppSettingsWindow.xaml`
- `AppSettingsWindow.xaml.cs`
- `MainWindow.xaml.cs`
- hotkey tests

However, first version does not require a dedicated Smart Prompt hotkey. Panel button is enough.

## Localization

Update all localization files:

- `AiteBar/Resources/Strings.resx`
- `AiteBar/Resources/Strings.ru.resx`
- `AiteBar/Resources/Strings.uk.resx`
- `AiteBar/Resources/Strings.de.resx`

Required keys:

    Tool_SmartPrompt
    SmartPrompt_Title
    SmartPrompt_AddApiKey
    SmartPrompt_ApiKeys
    SmartPrompt_Models
    SmartPrompt_Packages
    SmartPrompt_ImportPackage
    SmartPrompt_NewCard
    SmartPrompt_EditCard
    SmartPrompt_DuplicateCard
    SmartPrompt_DeleteCard
    SmartPrompt_SaveCard
    SmartPrompt_Send
    SmartPrompt_CopyResponse
    SmartPrompt_InputPlaceholder
    SmartPrompt_ResponsePlaceholder
    SmartPrompt_NoApiKeys
    SmartPrompt_NoCards
    SmartPrompt_NoActiveKey
    SmartPrompt_KeyActive
    SmartPrompt_KeyInvalid
    SmartPrompt_KeyRateLimited
    SmartPrompt_KeyUnknown
    SmartPrompt_AllKeysRateLimited
    SmartPrompt_InvalidPackage
    SmartPrompt_PackagePreviewTitle
    SmartPrompt_PackageImportSuccess
    SmartPrompt_PackageImportWarning
    SmartPrompt_PackageImportFailed
    SmartPrompt_ModelDisabled
    SmartPrompt_RequestFailed
    SmartPrompt_InvalidKeyOrNetwork
    SmartPrompt_ProviderAutoDetected
    SmartPrompt_ProviderManualSelection

Localization test must pass: every resource file must have the same keys and placeholders.

## Error Handling and Logging

User-facing errors must be short:

- Invalid key or network unavailable.
- No active key for this provider/model.
- Model is disabled or unavailable.
- All keys are currently rate-limited.
- AI Package file is invalid.
- Request failed. See log for details.

Technical errors go to `Logger`.

Never log:

- raw API keys;
- full Authorization headers;
- full request body if it contains user text;
- full response body if it may contain sensitive user data.

Allowed logs:

- provider name;
- masked key;
- HTTP status;
- error code;
- short sanitized message.

## Security Requirements

API keys must be encrypted at rest with DPAPI.

Package import must never execute code.

Package import must reject unsafe ZIP paths.

Prompt cards are text data only. Do not allow package to define commands, scripts, local file paths, browser launches, or AiteBar actions.

Do not store request/response history in first version.

Do not enable telemetry for prompt contents or API keys.

Do not send package contents anywhere except when the user explicitly sends a prompt request to an LLM provider.

## Non-Goals for First Version

Do not implement:

- cloud sync;
- marketplace UI;
- payment processing;
- license server;
- automatic downloading of paid packs;
- account creation;
- captcha bypass;
- provider policy bypass;
- chat history;
- prompt response database;
- file attachments;
- image input;
- audio input;
- streaming output;
- team sharing;
- remote pack update feed;
- encryption/signing of `.aitepkg` packages.

Package signatures and licensing can be a later ExecPlan.

## Milestone 1: Add Data Models and Storage

Implement the SmartPrompt folder, models, secret protection and storage service.

At the end of this milestone, tests should prove that state can be saved and loaded, API keys are not stored in plain text, and corrupted JSON is handled safely.

Work:

- Create `AiteBar/SmartPrompt/SmartPromptModels.cs`.
- Create `SmartPromptSecretProtector.cs`.
- Create `SmartPromptStorageService.cs`.
- Update `PathHelper.cs`.
- Add tests in `AiteBar.Tests/SmartPromptStorageServiceTests.cs`.

Validation:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter SmartPromptStorageServiceTests

Expected:

    Passed SmartPromptStorageServiceTests

## Milestone 2: Provider Detection and Model Classification

Implement deterministic non-network logic.

Work:

- Create `SmartPromptProviderDetector.cs`.
- Create `SmartPromptModelClassifier.cs`.
- Add tests for all provider regexes and model classes.

Validation:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter SmartPromptProviderDetectorTests
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter SmartPromptModelClassifierTests

Expected:

    All tests pass.

## Milestone 3: AI Package Import

Implement `.aitepkg` / `.aitepromptpack` reading and validation.

Work:

- Create `SmartPromptPackageManifest` models.
- Create `SmartPromptPackService.cs`.
- Implement preview reading.
- Implement import into `SmartPromptState`.
- Implement duplicate/update rules.
- Implement safe ZIP path validation.
- Add tests.

Validation:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter SmartPromptPackServiceTests

Expected:

- valid package imports cards;
- invalid JSON fails safely;
- missing fields skip invalid card;
- unsafe ZIP paths are rejected;
- reimport does not duplicate package cards;
- user cards are not deleted.

## Milestone 4: Provider Clients and Request Service

Implement network layer behind abstractions.

Work:

- Create `ISmartPromptProviderClient`.
- Create OpenAI-compatible client.
- Create Google client.
- Create request service.
- Create key pool service.
- Add tests for key pool behavior.

Tests should avoid real network calls. Use fake provider clients or fake `HttpMessageHandler`.

Validation:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter SmartPromptKeyPoolServiceTests

Expected:

- active key selected;
- rate-limited key skipped;
- expired cooldown key reused;
- keys sorted by `LastUsedAt`;
- retry stops after all eligible keys.

## Milestone 5: Smart Prompt Window

Implement the WPF UI.

Work:

- Create `SmartPromptWindow.xaml`.
- Create `SmartPromptWindow.xaml.cs`.
- Load state on open.
- Show key management.
- Show package import.
- Show card list.
- Show card editor.
- Implement one input field and response area.
- Implement Send and Ctrl+Enter.
- Implement Copy response.

Manual validation:

- open window;
- create card;
- close and reopen;
- card persists;
- import package;
- package cards appear;
- invalid key shows error;
- no active key shows error on send.

## Milestone 6: AiteBar Integration

Register utility and add panel button.

Work:

- Create `SmartPromptUtility.cs`.
- Update `MainWindow.xaml`.
- Update `MainWindow.xaml.cs`.
- Update `Models.cs`.
- Update `AppSettingsWindow.xaml` and `.cs`.
- Update localization resources.
- Update tests affected by localization.

Manual validation:

- start AiteBar;
- panel still appears and hides correctly;
- Smart Prompt button appears;
- button opens Smart Prompt window;
- visibility checkbox hides/shows button;
- existing utilities still work.

## Milestone 7: Full Build and Regression

Run full build and test suite.

Commands from repository root:

    dotnet build .\AiteBar.sln -c Release

Expected:

    Build succeeded.
    0 Error(s)

Then:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If WPF temp-file issues occur:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

Manual checks:

- panel Top/Bottom/Left/Right still works;
- panel animation still works;
- tray menu still works;
- existing utility buttons still work;
- Smart Prompt Client opens from panel;
- package import works;
- manual card creation works;
- no raw key appears in JSON or logs.

## Validation and Acceptance

The feature is accepted only when all these conditions are true:

1. `dotnet build .\AiteBar.sln -c Release` succeeds.
2. `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` succeeds, or documented WPF temp issue uses `dotnet vstest` fallback successfully.
3. Smart Prompt Client opens from AiteBar panel.
4. Smart Prompt Client can be hidden/shown from AiteBar settings.
5. User can add an API key.
6. Provider can be auto-detected for supported key patterns.
7. API key is not stored as plain text.
8. User can create, edit, duplicate and delete prompt cards.
9. User can import `.aitepkg` package.
10. `.aitepromptpack` alias is accepted.
11. Invalid package does not crash app.
12. Package update does not delete user-created cards.
13. User can select card, input text, send request and copy response.
14. Rate-limited key is skipped.
15. All-key exhaustion shows clear message and stops retrying.
16. Raw API keys are never written to logs.
17. All localization resource files contain matching keys.
18. Existing AiteBar panel layout, animation, contexts and utilities are not broken.

## Idempotence and Recovery

All file and directory creation must be idempotent.

Repeated package import must not create endless duplicates.

If save fails, current in-memory state must remain available until window closes.

If storage JSON is corrupted, rename it and create empty state.

If network request fails, user input must remain in the input field.

If provider validation fails, key must not be marked active.

If package import fails halfway, no partial corrupt state should remain. Prefer building import result in memory and saving only after validation.

## Progress

- [ ] Initial ExecPlan created.
- [ ] Implementation not started.

## Surprises & Discoveries

No discoveries yet. Add entries here while implementing.

Example format:

- Observation: `UtilityRegistry.RegisterAllFromAssembly()` only scans current assembly at startup.
  Evidence: found in `AiteBar/UtilityRegistry.cs` while integrating `SmartPromptUtility`.

## Decision Log

- Decision: Use `.aitepkg` as the main public package extension and accept `.aitepromptpack` as an alias.
  Rationale: `.aitepkg` is broader and allows future AI resources beyond prompt cards. `.aitepromptpack` remains useful as a descriptive compatibility format.
  Date/Author: 2026-06-29 / Codex implementer must preserve unless repository constraints force change.

- Decision: Implement Smart Prompt Client as a built-in AiteBar utility, not as a separate app.
  Rationale: AiteBar already has a utility system and the product value is quick access from the edge panel.
  Date/Author: 2026-06-29 / Codex implementer must preserve.

- Decision: Do not implement automatic account creation or hidden provider-limit evasion.
  Rationale: The utility may rotate user-provided keys for reliability, but must not create or bypass provider accounts or policies.
  Date/Author: 2026-06-29 / Codex implementer must preserve.

- Decision: Do not persist prompt request history in first version.
  Rationale: Reduces privacy risk and keeps the first version focused on cards, packages, keys and request execution.
  Date/Author: 2026-06-29 / Codex implementer must preserve.

## Outcomes & Retrospective

No implementation outcome yet. At completion, summarize:

- what was implemented;
- what tests were added;
- what manual validation was performed;
- what limitations remain;
- what should become the next ExecPlan.

## Artifacts and Notes

Example `.aitepkg` manifest for testing:

    {
      "formatVersion": 1,
      "packageId": "developer-toolkit",
      "type": "ai-toolkit",
      "name": "AI Developer Toolkit",
      "version": "1.0.0",
      "author": "Codebdbd",
      "description": "Professional prompt cards for developers.",
      "cards": [
        {
          "name": "Code Review",
          "category": "Development",
          "description": "Find bugs and risks in pasted code.",
          "systemPrompt": "You are a senior software engineer. Review the provided code. Return concrete issues, risks and improvements. Avoid filler.",
          "provider": "groq",
          "modelId": "llama-3.3-70b-versatile",
          "temperature": 0.2,
          "maxOutputTokens": 4096,
          "iconGlyph": "\uE943",
          "iconColor": "#007ACC"
        },
        {
          "name": "SQL Generator",
          "category": "Development",
          "description": "Generate SQL from a plain-language task.",
          "systemPrompt": "You are a senior database engineer. Generate safe, readable SQL for the user request. Explain assumptions briefly.",
          "provider": "",
          "modelId": "llama-3.3-70b-versatile",
          "temperature": 0.1,
          "maxOutputTokens": 2048,
          "iconGlyph": "\uE8EE",
          "iconColor": "#007ACC"
        }
      ]
    }

Expected `smart_prompt.json` must not contain raw API key text. It may contain:

    {
      "keys": [
        {
          "provider": "groq",
          "encryptedApiKey": "AQAAANCMnd8BFdERjHoAwE...",
          "maskedApiKey": "gsk_abcd...wxyz",
          "status": "active"
        }
      ]
    }
