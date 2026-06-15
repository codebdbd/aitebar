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

Пока системные утилиты на панели подключаются явно. Для новой кнопки нужно обновить:

- `AiteBar/MainWindow.xaml`: добавить `Button` в `SystemUtilsPanel`.
- `AiteBar/MainWindow.xaml.cs`: добавить tooltip в `ApplyLocalizedText()`.
- `AiteBar/MainWindow.xaml.cs`: добавить context menu в `AttachSystemUtilityContextMenus()`.
- `AiteBar/MainWindow.xaml.cs`: добавить настройку в `GetVisibleSystemButtonCount()`.
- `AiteBar/MainWindow.xaml.cs`: добавить кнопку в `ApplySystemUtilityVisibility()` и расчет `hasSystemUtils`.
- `AiteBar/MainWindow.xaml.cs`: добавить обработчик клика:

```csharp
private async void BtnMyNewUtility_Click(object sender, RoutedEventArgs e)
{
    await RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("MyNewUtility", HideDock));
}
```

Если кнопка должна участвовать в keyboard focus traversal, добавьте ее в `EnumeratePanelButtons()`.

## Настройка видимости

Добавьте boolean-настройку в `AppSettings`:

```csharp
public bool ShowPresetMyNewUtility { get; set; } = true;
```

Затем подключите ее в:

- `AiteBar/AppSettingsWindow.xaml`: чекбокс во вкладке быстрых утилит.
- `AiteBar/AppSettingsWindow.xaml.cs`: загрузка значения в `LoadSettings()`.
- `AiteBar/AppSettingsWindow.xaml.cs`: сохранение значения в `BtnSave_Click()`.

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
- `ColorPickerUtility.cs`: modal/dialog-like одноразовый запуск без `UtilityBase<TWindow>`.
- `FileSorterUtility.cs`: сервисная логика отдельно от окна.
- `IconConverterUtility.cs`: конвертация PNG/JPG/WEBP/BMP/TIFF/SVG в Windows ICO через SkiaSharp/Svg.Skia, отдельный сервис и encoder; preview работает отдельно от финальной сборки ICO, входные файлы валидируются по размеру/безопасности.
