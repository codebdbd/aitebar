# Добавление новых утилит в AiteBar

Этот документ описывает фактическую схему подключения быстрых встроенных утилит в AiteBar.

## Архитектура

Утилиты автоматически регистрируются через `UtilityRegistry` и реализуют `IUtility` из `AiteBar/UtilityRegistry.cs`. Для обычной утилиты с отдельным WPF-окном предпочтительно наследоваться от `UtilityBase<TWindow>`: базовый класс уже умеет активировать открытое окно, вызывать `onBeforeExecute`, создавать окно и очищать ссылку после закрытия.

Каждая утилита должна быть помечена атрибутом `[Utility]` для автоматической регистрации.

### Версионирование контрактов:
- `ContractVersion`: версия контракта утилиты (по умолчанию 1.0)
- `IsCompatibleWith(Version coreVersion)`: проверка совместимости с ядром (по умолчанию true)

### Изоляция ошибок:
- Крах утилиты не крашит всё приложение
- Логирование и телеметрия отправляется
- Пользователь видит понятное сообщение вместо краш

Минимальный класс утилиты:

```csharp
using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class MyNewUtility : UtilityBase<MyNewUtilityWindow>
{
    public override string Id => "MyNewUtility";
    public override string DisplayNameKey => "Tool_MyNewUtility";
    public override string IconGlyph => "\uE946";
    public override string IconColor => "#007ACC";

    // Опционально: можно переопределить версию контракта и проверку совместимости
    public override Version ContractVersion => new(1, 0);
    public override bool IsCompatibleWith(Version coreVersion) => true;

    protected override MyNewUtilityWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        return new MyNewUtilityWindow(settingsService) { Owner = owner };
    }

    protected override void ShowWindow(MyNewUtilityWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }
}
```

Если утилита не имеет постоянного окна, можно реализовать `IUtility` напрямую. Пример: `ColorPickerUtility`.

## Подключение

1. Создайте WPF-окно утилиты: `MyNewUtilityWindow.xaml` и `MyNewUtilityWindow.xaml.cs`.
2. Создайте класс `MyNewUtility` с атрибутом `[Utility]`.
3. Утилита будет автоматически зарегистрирована из текущей сборки через `UtilityRegistry.RegisterAllFromAssembly()`.
4. Добавьте локализацию во все ресурсы:
   - `AiteBar/Resources/Strings.resx`
   - `AiteBar/Resources/Strings.ru.resx`
   - `AiteBar/Resources/Strings.uk.resx`
   - `AiteBar/Resources/Strings.de.resx`

Тест `LocalizationServiceTests.ResourceFiles_HaveSameKeysAndFormatPlaceholders` требует одинаковый набор ключей во всех ресурсных файлах.

## Кнопка на панели

Начиная с версии с рефакторингом unified buttons, системные утилиты регистрируются единым способом через централизованный каталог. Для добавления новой кнопки утилиты нужно обновить следующие 6 точек:

1. **`AiteBar/UtilityButtonCatalog.cs** — добавить статическое свойство `UtilityButtonDefinition MyAi` (Id, PresetFlag nameof(AppSettings.ShowPresetMyAi), Glyph, DefaultOrder, LocTitleKey, LocTooltipKey) и включить его в массив `All`.

2. **`AiteBar/Models.cs`** — в класс `AppSettings` добавить:
   ```csharp
   public bool ShowPresetMyAi { get; set; } = true; // или false если по умолчанию скрыто
   ```

3. **`AiteBar/AppSettingsService.cs`** — в `Clone()` сделать deep-copy нового ShowPreset-флага (аналогично другим ShowPreset*).

4. **`AiteBar/AppSettingsWindow.xaml.cs** — в методе `GetUtilityVisibilityBindings()` добавить binding нового чекбокса:
   ```csharp
   (nameof(AppSettings.ShowPresetMyAi), "Tool_MyAi_Title")
   ```

5. **`AiteBar/AppSettingsWindow.xaml`** — добавить `CheckBox` в секцию `Quick Tools` вкладки `Quick Tools` с локализацией `{local:Loc ResourceKey=Tool_MyAi_Title}`.

6. **`AiteBar/MainWindow.xaml.cs`** — в `ExecuteUnifiedButtonActionAsync()` добавить `case "MyAi"` с запуском:
   ```csharp
   case "MyAi":
       _ = _actionService.LaunchUtilityAsync("MyAi", HideDock);
       break;
   ```

Если кнопка должна участвовать в keyboard focus traversal, дополнительно включите ее имя в списки обхода фокуса — `UnifiedButtonService` и `UtilityButtonCatalog` автоматически подхватывают кнопку из `All`.

Для AI-утилит (с внешним AI-шлюзом) дополнительно см. [creating-ai-utilities.md](creating-ai-utilities.md) — там описана интеграция AiGateway, шаблон окна и соглашения об автосохранении состояния.

## Настройка видимости

Добавьте boolean-настройку в `AppSettings`:

```csharp
public bool ShowPresetMyNewUtility { get; set; } = true;
```

Затем подключите ее в:

- `AiteBar/AppSettingsWindow.xaml.cs`: binding в `GetUtilityVisibilityBindings()`.
- `AiteBar/AppSettingsService.cs`: clone в `Clone()`.
- `AiteBar/AppSettingsWindow.xaml`: чекбокс во вкладке быстрых утилит (если используется ручной binding вместо GetUtilityVisibilityBindings — не требуется, если метод уже используется).

## Горячие клавиши

Хоткеи для утилит не создаются автоматически. Если утилите нужен глобальный хоткей, обновите:

- `AiteBar/Models.cs`: новое свойство `HotkeyBinding`.
- `AiteBar/HotkeyService.cs`: `HotkeyCommand`, уникальный ID, descriptor и binding mapping.
- `AiteBar/AppSettingsWindow.xaml`: controls для выбора hotkey.
- `AiteBar/AppSettingsWindow.xaml.cs`: загрузка, валидация и сохранение.
- `AiteBar/MainWindow.xaml.cs`: запуск в `ExecuteHotkeyCommand()`.
- `AiteBar.Tests/HotkeyServiceTests.cs`: ожидаемый список команд и mapping.

## Примеры

- `QuickNoteUtility.cs`: окно со своим позиционированием и сохранением состояния.
- `TimerStopwatchUtility.cs`: окно рядом с панелью.
- `ColorPickerUtility.cs`: modal/dialog-like одноразовый запуск без `UtilityBase<TWindow>.
- `FileSorterUtility.cs`: сервисная логика отдельно от окна.
- `IconConverterUtility.cs`: конвертация PNG/JPG/WEBP/BMP/TIFF/SVG в Windows ICO через SkiaSharp/Svg.Skia, отдельный сервис и encoder; preview работает отдельно от финальной сборки ICO, входные файлы валидируются по размеру/безопасности.
- `ClipboardManagerUtility.cs`: утилита с отдельным runtime-history сервисом, множеством контекстных действий над клипом, persistence истории между сессиями.
- `TextProcessingUtility.cs`: AI-утилита с потоковым SSE-выводом через AiGateway, Diff, Undo/Redo, защита технических фрагментов.
- `ZenEditorUtility.cs`: отдельное полноэкранное окно с multi-document store, поиском, темами, экспортом TXT и корзиной.
- `PromptBuilderUtility.cs`: комплексная AI-утилита с 11 рубриками, каталогом стилей, кэшем моделей AiGateway и сохранением draft-ов (PromptBuilderDrafts) для каждого направления.
