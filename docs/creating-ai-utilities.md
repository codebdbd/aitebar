# Создание утилит с использованием AI API в AiteBar

Полный гайд по добавлению утилиты, интегрированной с нейросетевыми API.

---

## Содержание

1. [Обзор архитектуры](#1-обзор-архитектуры)
2. [Необходимые файлы](#2-необходимые-файлы)
3. [Шаг 1: Создание класса утилиты](#3-шаг-1-создание-класса-утилиты)
4. [Шаг 2: Создание окна утилиты](#4-шаг-2-создание-окна-утилиты)
5. [Шаг 3: Регистрация в каталоге](#5-шаг-3-регистрация-в-каталоге)
6. [Шаг 4: Настройки видимости](#6-шаг-4-настройки-видимости)
7. [Шаг 5: Привязка к панели](#7-шаг-5-привязка-к-панели)
8. [Шаг 6: Локализация](#8-шаг-6-локализация)
9. [Использование AiGateway](#9-использование-aigateway)
10. [Работа с моделями и capabilities](#10-работа-с-моделями-и-capabilities)
11. [Обработка ошибок и failover](#11-обработка-ошибок-и-failover)
12. [Чеклист перед коммитом](#12-чеклист-перед-коммитом)
13. [Полный пример](#13-полный-пример)

---

## 1. Обзор архитектуры

AI-стек AiteBar состоит из 4 слоёв:

```
┌──────────────────────────────────────────────────────────┐
│  Utility Window (UI)                                     │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Ввод prompt → отображение результата              │  │
│  └────────────────────────────────────────────────────┘  │
├──────────────────────────────────────────────────────────┤
│  AiGateway — оркестрация, failover, кэширование моделей │
│  • Автоматический выбор соединения по приоритету         │
│  • Кэш моделей (TTL 15 минут)                           │
│  • Cooldown при ошибках (429, 402, 5xx)                 │
│  • FreeTierOnly фильтрация                              │
├──────────────────────────────────────────────────────────┤
│  AiProviderClient — HTTP-запросы к провайдерам          │
│  • 4 протокола: OpenAI-compatible, OpenRouter, Gemini,  │
│    GitHub Models                                         │
│  • Парсинг моделей и генерация ответов                  │
├──────────────────────────────────────────────────────────┤
│  WindowsAiCredentialStore — безопасное хранение ключей  │
│  • Win32 Credential Manager (CredWriteW/CredReadW)      │
│  • Ключи не попадают в settings.json                    │
└──────────────────────────────────────────────────────────┘
```

### Поддерживаемые провайдеры

| Провайдер | Протокол | Free Tier | Примечание |
|---|---|---|---|
| OpenRouter | OpenRouter | Частично | Агрегатор, `openrouter/free` модель |
| Cerebras | OpenAI-compatible | Да | Быстрые инференсы |
| Google Gemini | Gemini | Да | API key в query string |
| Groq | OpenAI-compatible | Да | Быстрые инференсы |
| GitHub Models | GitHub Models | Да | Требует PAT |
| Mistral AI | OpenAI-compatible | Да | — |

---

## 2. Необходимые файлы

Для добавления новой AI-утилиты нужно создать/изменить:

**Создать:**
- `AiteBar/MyAiUtility.cs` — класс утилиты
- `AiteBar/MyAiWindow.xaml` + `.cs` — окно утилиты

**Изменить:**
- `AiteBar/UtilityButtonCatalog.cs` — запись каталога
- `AiteBar/Models.cs` — `ShowPresetMyAi` свойство
- `AiteBar/AppSettingsService.cs` — clone-строка
- `AiteBar/AppSettingsWindow.xaml` — чекбокс видимости
- `AiteBar/AppSettingsWindow.xaml.cs` — привязка чекбокса
- `AiteBar/MainWindow.xaml.cs` — case в switch + hotkey
- `AiteBar/Resources/Strings.resx` — локализация

---

## 3. Шаг 1: Создание класса утилиты

### Минимальная реализация (с окном)

```csharp
using System.Windows;
using System.Runtime.Versioning;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class MyAiUtility : UtilityBase<MyAiWindow>
{
    public override string Id => "MyAi";
    public override string DisplayNameKey => "Tool_MyAi";
    public override string IconGlyph => "\uE99A";  // Unicode глиф Fluent System Icons
    public override string IconColor => "#7C3AED";

    protected override MyAiWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        // Передаём AiGateway через MainWindow
        var mainWindow = Application.Current?.MainWindow as MainWindow;
        AiGateway? aiGateway = mainWindow?.GetAiGateway();

        return new MyAiWindow(aiGateway, settingsService) { Owner = owner };
    }

    protected override void ShowWindow(MyAiWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }
}
```

### Альтернатива: без окна (одноразовое действие)

```csharp
[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class QuickAiAction : IUtility
{
    public string Id => "QuickAiAction";
    public string DisplayNameKey => "Tool_QuickAiAction";
    public string IconGlyph => "\uE99A";
    public string IconColor => "#7C3AED";

    public async Task LaunchAsync(AppSettingsService settingsService, Window? owner, Func<Task>? onBeforeExecute = null)
    {
        try
        {
            if (onBeforeExecute != null) await onBeforeExecute();

            var mainWindow = Application.Current?.MainWindow as MainWindow;
            AiGateway? gateway = mainWindow?.GetAiGateway();
            if (gateway == null) return;

            // Быстрое действие — например, перевод текста из буфера
            string text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text)) return;

            var response = await gateway.GenerateAsync(new AiChatRequest
            {
                Messages = [new("user", $"Переведи на английский: {text}")],
                MaxOutputTokens = 512
            });

            Clipboard.SetText(response.Content);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }
}
```

### Ключевые правила

| Требование | Описание |
|---|---|
| `[Utility]` атрибут | **Обязателен** — без него рефлексия не найдёт класс |
| `Id` | Уникальный строковый ключ, совпадает с ключом в `UtilityButtonCatalog` |
| `DisplayNameKey` | Ключ из `Strings.resx` для отображаемого имени |
| `IconGlyph` | Unicode-глиф из Fluent System Icons (`\uXXXX`) |
| `IconColor` | Hex-цвет акцента кнопки |

---

## 4. Шаг 2: Создание окна утилиты

### XAML

```xml
<local:DarkWindow x:Class="AiteBar.MyAiWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="clr-namespace:AiteBar"
    Title="My AI Tool"
    Width="480" Height="520"
    WindowStartupLocation="Manual"
    Background="#1A1A1C"
    Foreground="White">

    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- Заголовок -->
            <RowDefinition Height="*"/>     <!-- Область ввода -->
            <RowDefinition Height="Auto"/>  <!-- Кнопки -->
            <RowDefinition Height="*"/>     <!-- Результат -->
        </Grid.RowDefinitions>

        <!-- Заголовок -->
        <TextBlock Grid.Row="0" Text="AI Tool" FontSize="18" FontWeight="SemiBold"
                   Margin="0,0,0,12"/>

        <!-- Prompt -->
        <TextBox x:Name="TxtPrompt" Grid.Row="1"
                 AcceptsReturn="True" TextWrapping="Wrap"
                 VerticalScrollBarVisibility="Auto"
                 Background="#252526" Foreground="White"
                 BorderBrush="#3F3F46" BorderThickness="1"
                 Padding="8" Margin="0,0,0,8"/>

        <!-- Кнопки -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right"
                    Margin="0,0,0,8">
            <Button x:Name="BtnGenerate" Content="Generate"
                    Padding="16,6" Margin="0,0,8,0"
                    Click="BtnGenerate_Click"/>
            <Button x:Name="BtnCopy" Content="Copy"
                    Padding="16,6"
                    Click="BtnCopy_Click"/>
        </StackPanel>

        <!-- Результат -->
        <Border Grid.Row="3" Background="#252526" BorderBrush="#3F3F46"
                BorderThickness="1" CornerRadius="4" Padding="8">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <TextBlock x:Name="TxtResult" TextWrapping="Wrap"
                           Foreground="#D4D4D4"/>
            </ScrollViewer>
        </Border>
    </Grid>
</local:DarkWindow>
```

### Code-behind

```csharp
using System.Windows;

namespace AiteBar;

public partial class MyAiWindow : DarkWindow
{
    private readonly AiGateway? _aiGateway;
    private readonly AppSettingsService _settingsService;

    public MyAiWindow(AiGateway? aiGateway, AppSettingsService settingsService)
    {
        _aiGateway = aiGateway;
        _settingsService = settingsService;
        InitializeComponent();
    }

    public void ShowNearPanel(AppSettingsService settingsService)
    {
        // Позиционирование рядом с панелью (по аналогии с другими утилитами)
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow != null)
        {
            Left = mainWindow.Left;
            Top = mainWindow.Top + mainWindow.Height + 4;
        }
        Show();
    }

    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (_aiGateway == null)
        {
            TxtResult.Text = "AI not configured. Add a connection in Settings → AI.";
            return;
        }

        string prompt = TxtPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        BtnGenerate.IsEnabled = false;
        TxtResult.Text = "Generating...";

        try
        {
            var response = await _aiGateway.GenerateAsync(new AiChatRequest
            {
                Messages = [new("user", prompt)],
                MaxOutputTokens = 2048,
                Temperature = 0.7
            });

            TxtResult.Text = response.Content;
        }
        catch (InvalidOperationException ex)
        {
            TxtResult.Text = $"Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            TxtResult.Text = "An error occurred.";
        }
        finally
        {
            BtnGenerate.IsEnabled = true;
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtResult.Text))
        {
            Clipboard.SetText(TxtResult.Text);
        }
    }
}
```

### Позиционирование окна

Два варианта:

```csharp
// Вариант 1: ShowNearPanel — рядом с панелью (для крупных утилит)
public void ShowNearPanel(AppSettingsService settingsService)
{
    var mainWindow = Application.Current?.MainWindow;
    if (mainWindow != null)
    {
        Left = mainWindow.Left;
        Top = mainWindow.Top + mainWindow.Height + 4;
    }
    Show();
}

// Вариант 2: ShowSimple — по позиции из настроек (для компактных утилит)
public void ShowSimple(AppSettings settings)
{
    // Восстановление позиции из settings или позиционирование по умолчанию
    Show();
}
```

---

## 5. Шаг 3: Регистрация в каталоге

В `UtilityButtonCatalog.cs` добавить статическое свойство и элемент в `All`:

```csharp
// Добавить свойство:
public static UtilityButtonDefinition MyAi { get; } = new(
    "MyAi",                        // Id — совпадает с IUtility.Id
    "\uE99A",                       // IconGlyph
    "#7C3AED",                      // IconColor
    "Main_MyAiTooltip",            // TooltipKey — ключ локализации
    settings => settings.ShowPresetMyAi,                    // VisibilityGetter
    (settings, visible) => settings.ShowPresetMyAi = visible); // VisibilitySetter

// Добавить в массив All:
public static IReadOnlyList<UtilityButtonDefinition> All { get; } =
[
    // ... существующие утилиты ...
    MyAi                           // ← добавить сюда
];
```

---

## 6. Шаг 4: Настройки видимости

### Models.cs

Добавить свойство в `AppSettings`:

```csharp
public bool ShowPresetMyAi { get; set; } = false;  // false = скрыта по умолчанию
```

### AppSettingsService.cs

Добавить clone-строку в метод клонирования:

```csharp
ShowPresetMyAi = original.ShowPresetMyAi,
```

### AppSettingsWindow.xaml

Добавить CheckBox на вкладку Utilities:

```xml
<CheckBox x:Name="ChkShowPresetMyAi" Content="My AI Tool"
          Style="{StaticResource SettingsCheckBoxStyle}"
          Margin="0,2"/>
```

### AppSettingsWindow.xaml.cs

Добавить привязку в `GetUtilityVisibilityBindings()`:

```csharp
(ChkShowPresetMyAi, UtilityButtonCatalog.MyAi),
```

---

## 7. Шаг 5: Привязка к панели

### MainWindow.xaml.cs

Добавить case в `ExecuteUnifiedButtonActionAsync()` (около строки 1580):

```csharp
case "MyAi":
    await _actionService.LaunchUtilityAsync("MyAi", HideDock);
    break;
```

Добавить hotkey command (если нужна горячая клавиша):

```csharp
// В enum HotkeyCommand добавить:
MyAi,

// В switch в HandleHotkeyCommand():
case HotkeyCommand.MyAi:
    _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("MyAi", HideDock));
    break;
```

Добавить обработчик клика (около строки 1949):

```csharp
private async void BtnMyAi_Click(object sender, RoutedEventArgs e)
{
    await RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("MyAi", HideDock));
}
```

---

## 8. Шаг 6: Локализация

Добавить строки в все файлы `Strings*.resx`:

| Ключ | Значение (en) | Значение (ru) |
|---|---|---|
| `Tool_MyAi` | My AI Tool | Мой AI инструмент |
| `Main_MyAiTooltip` | My AI Tool | Мой AI инструмент |
| `MyAi_Generating` | Generating... | Генерация... |
| `MyAi_Error` | An error occurred | Произошла ошибка |
| `MyAi_NoConnection` | AI not configured | AI не настроен |

---

## 9. Использование AiGateway

### Получение экземпляра

`AiGateway` создаётся в `MainWindow` и доступен через публичный метод:

```csharp
// В utility window constructor:
var mainWindow = Application.Current?.MainWindow as MainWindow;
AiGateway? aiGateway = mainWindow?.GetAiGateway();
```

### Базовый вызов

```csharp
var response = await _aiGateway.GenerateAsync(new AiChatRequest
{
    Messages = [
        new("system", "You are a helpful translator."),
        new("user", "Translate to English: Привет мир")
    ],
    MaxOutputTokens = 1024,
    Temperature = 0.3
});

string result = response.Content;
// response.ProviderId, response.ModelId, response.PromptTokens, response.CompletionTokens
```

### Системный промпт

```csharp
var request = new AiChatRequest
{
    Messages = [
        new("system", "Ты — помощник по коду. Отвечай кратко и по делу."),
        new("user", userPrompt)
    ],
    MaxOutputTokens = 2048,
    RequiredCapabilities = AiCapabilities.Text
};
```

### Multi-turn диалог

```csharp
var messages = new List<AiChatMessage>
{
    new("system", "You are a coding assistant."),
    new("user", "How do I read a file in C#?"),
    new("assistant", "Use File.ReadAllText(path) or StreamReader..."),
    new("user", "And write?")  // следующий вопрос
};

var response = await _aiGateway.GenerateAsync(new AiChatRequest
{
    Messages = messages,
    MaxOutputTokens = 1024
});
```

---

## 10. Работа с моделями и capabilities

### AiCapabilities

Флаги для фильтрации моделей:

```csharp
[Flags]
public enum AiCapabilities
{
    None = 0,
    Text = 1,           // Базовый текстовый inference
    Vision = 2,         // Приём изображений
    Streaming = 4,      // Стриминг (пока не используется)
    Tools = 8,          // Function calling
    StructuredOutput = 16,  // JSON structured output
    Reasoning = 32      // Thinking/reasoning модели
};
```

### Фильтрация по capabilities

```csharp
// Только текстовые модели:
RequiredCapabilities = AiCapabilities.Text

// Модели с vision:
RequiredCapabilities = AiCapabilities.Text | AiCapabilities.Vision

// Модели с reasoning (thinking):
RequiredCapabilities = AiCapabilities.Text | AiCapabilities.Reasoning
```

### FreeTierOnly

По умолчанию `AiSettings.FreeTierOnly = true` — Gateway выбирает только бесплатные модели. Для paid-моделей нужно выключить в настройках или установить `FreeTierOnly = false` программно.

### Выбор конкретной модели

```csharp
var response = await _aiGateway.GenerateAsync(new AiChatRequest
{
    Messages = [...],
    PreferredProviderId = "openrouter",  // предпочтительный провайдер
    PreferredModelId = "openrouter/free" // конкретная модель
});
```

Порядок fallback:
1. `PreferredModelId` из запроса
2. `PreferredModelId` из `AiConnectionSettings`
3. Для OpenRouter: `openrouter/free`
4. Первая доступная модель

---

## 11. Обработка ошибок и failover

### Автоматический failover

`AiGateway` автоматически перебирает все доступные соединения:

```
Соединение 1 (OpenRouter) → 429 Too Many Requests → cooldown 1 мин
Соединение 2 (Groq) → 200 OK → результат
```

### Cooldown таблица

| HTTP код | Состояние | Cooldown |
|---|---|---|
| 401 | `InvalidCredential` | Перманент (пока не исправлен) |
| 403 | `PermissionDenied` | Перманент |
| 429 | `CoolingDown` | Retry-After или 1 минута |
| 402 | `QuotaExhausted` | 24 часа |
| 5xx / сеть | `Unavailable` | 30 секунд |

### Ручная обработка ошибок

```csharp
try
{
    var response = await _aiGateway.GenerateAsync(request);
    TxtResult.Text = response.Content;
}
catch (InvalidOperationException ex) when (ex.Message.Contains("No enabled AI connections"))
{
    TxtResult.Text = "Add an AI connection in Settings → AI tab.";
}
catch (InvalidOperationException ex)
{
    TxtResult.Text = $"AI error: {ex.Message}";
}
```

### Сброс состояния соединения

```csharp
// Сброс cooldown для конкретного соединения:
_aiGateway.ResetConnection(connectionId);
```

---

## 12. Чеклист перед коммитом

### Код

- [ ] Класс утилиты помечен `[Utility]`
- [ ] `Id` совпадает с ключом в `UtilityButtonCatalog`
- [ ] `DisplayNameKey` и tooltip ключ добавлены в `Strings.resx` (en + ru + de + uk)
- [ ] `UtilityButtonCatalog.All` содержит новую запись
- [ ] `AppSettings` содержит `ShowPresetMyAi`
- [ ] `AppSettingsService` clone-строка обновлена
- [ ] `AppSettingsWindow` содержит привязку чекбокса
- [ ] `MainWindow.xaml.cs` содержит case в `ExecuteUnifiedButtonActionAsync`
- [ ] Hotkey добавлен (если нужен)

### AI интеграция

- [ ] `AiGateway` получен через `MainWindow.GetAiGateway()`
- [ ] Обработаны ошибки (нет соединения, нет моделей, таймаут)
- [ ] `RequiredCapabilities` установлены корректно
- [ ] Текст промпта не содержит секретов

### Тесты

- [ ] `dotnet build AiteBar.sln -c Release` — собирается
- [ ] `dotnet test AiteBar.Tests -c Release` — тесты проходят
- [ ] Добавлен unit-тест для не-UI логики (если есть)

### Ручная проверка

- [ ] Утилита появляется на панели при включённой видимости
- [ ] Утилита скрывается при выключенной видимости
- [ ] AI запрос выполняется успешно
- [ ] Ошибки отображаются в UI
- [ ] Кнопка "Generate" блокируется на время запроса
- [ ] Горячая клавиша работает (если добавлена)

---

## 13. Полный пример

### MyAiUtility.cs

```csharp
using System.Windows;
using System.Runtime.Versioning;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class MyAiUtility : UtilityBase<MyAiWindow>
{
    public override string Id => "MyAi";
    public override string DisplayNameKey => "Tool_MyAi";
    public override string IconGlyph => "\uE99A";
    public override string IconColor => "#7C3AED";

    protected override MyAiWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        var mainWindow = Application.Current?.MainWindow as MainWindow;
        AiGateway? aiGateway = mainWindow?.GetAiGateway();
        return new MyAiWindow(aiGateway, settingsService) { Owner = owner };
    }

    protected override void ShowWindow(MyAiWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }
}
```

### MyAiWindow.xaml

```xml
<local:DarkWindow x:Class="AiteBar.MyAiWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="clr-namespace:AiteBar"
    Title="My AI Tool" Width="480" Height="520"
    WindowStartupLocation="Manual"
    Background="#1A1A1C" Foreground="White">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="AI Tool" FontSize="18" FontWeight="SemiBold" Margin="0,0,0,12"/>

        <TextBox x:Name="TxtPrompt" Grid.Row="1" AcceptsReturn="True" TextWrapping="Wrap"
                 VerticalScrollBarVisibility="Auto" Background="#252526" Foreground="White"
                 BorderBrush="#3F3F46" BorderThickness="1" Padding="8" Margin="0,0,0,8"
                 local:DarkWindow.Watermark="Enter your prompt..."/>

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,0,0,8">
            <Button x:Name="BtnGenerate" Content="Generate" Padding="16,6" Margin="0,0,8,0"
                    Click="BtnGenerate_Click"/>
            <Button x:Name="BtnCopy" Content="Copy" Padding="16,6" Click="BtnCopy_Click"/>
        </StackPanel>

        <Border Grid.Row="3" Background="#252526" BorderBrush="#3F3F46"
                BorderThickness="1" CornerRadius="4" Padding="8">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <TextBlock x:Name="TxtResult" TextWrapping="Wrap" Foreground="#D4D4D4"/>
            </ScrollViewer>
        </Border>
    </Grid>
</local:DarkWindow>
```

### MyAiWindow.xaml.cs

```csharp
using System.Windows;

namespace AiteBar;

public partial class MyAiWindow : DarkWindow
{
    private readonly AiGateway? _aiGateway;
    private readonly AppSettingsService _settingsService;

    public MyAiWindow(AiGateway? aiGateway, AppSettingsService settingsService)
    {
        _aiGateway = aiGateway;
        _settingsService = settingsService;
        InitializeComponent();
    }

    public void ShowNearPanel(AppSettingsService settingsService)
    {
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow != null)
        {
            Left = mainWindow.Left;
            Top = mainWindow.Top + mainWindow.Height + 4;
        }
        Show();
    }

    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (_aiGateway == null)
        {
            TxtResult.Text = "AI not configured. Add a connection in Settings \u2192 AI.";
            return;
        }

        string prompt = TxtPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        BtnGenerate.IsEnabled = false;
        BtnCopy.IsEnabled = false;
        TxtResult.Text = "Generating...";

        try
        {
            var response = await _aiGateway.GenerateAsync(new AiChatRequest
            {
                Messages = [new("user", prompt)],
                MaxOutputTokens = 2048,
                Temperature = 0.7
            });

            TxtResult.Text = response.Content;
        }
        catch (InvalidOperationException ex)
        {
            TxtResult.Text = $"Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            TxtResult.Text = "An error occurred. Check logs.";
        }
        finally
        {
            BtnGenerate.IsEnabled = true;
            BtnCopy.IsEnabled = true;
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtResult.Text))
        {
            Clipboard.SetText(TxtResult.Text);
        }
    }
}
```

### UtilityButtonCatalog.cs (изменение)

```csharp
public static UtilityButtonDefinition MyAi { get; } = new(
    "MyAi", "\uE99A", "#7C3AED", "Main_MyAiTooltip",
    settings => settings.ShowPresetMyAi,
    (settings, visible) => settings.ShowPresetMyAi = visible);

// В массив All добавить:
MyAi,
```

### Models.cs (изменение)

```csharp
// В AppSettings добавить:
public bool ShowPresetMyAi { get; set; } = false;
```

### Strings.resx (добавить)

| Ключ | Значение |
|---|---|
| `Tool_MyAi` | My AI Tool |
| `Main_MyAiTooltip` | AI-powered assistant tool |
| `MyAi_Generating` | Generating... |
| `MyAi_NoConnection` | AI not configured. Add a connection in Settings. |

### Strings.ru.resx (добавить)

| Ключ | Значение |
|---|---|
| `Tool_MyAi` | Мой AI инструмент |
| `Main_MyAiTooltip` | AI-инструмент-помощник |
| `MyAi_Generating` | Генерация... |
| `MyAi_NoConnection` | AI не настроен. Добавьте подключение в Настройках. |
