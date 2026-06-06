# Добавление новых утилит в AiteBar

Этот документ описывает, как добавлять новые быстрые утилиты в AiteBar.

## Архитектура

Для управления утилитами используется центральный `UtilityRegistry` и интерфейс `IUtility`. Все утилиты реализуют этот интерфейс и регистрируются в реестре.

## Шаги добавления новой утилиты

### 1. Создайте класс утилиты

Создайте новый класс в корне проекта AiteBar, реализующий интерфейс `IUtility`:

```csharp
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public class MyNewUtility : IUtility
{
    private MyNewUtilityWindow? _window;

    // Уникальный идентификатор утилиты
    public string Id => "MyNewUtility";

    // Ключ локализованного имени (из Strings.resx)
    public string DisplayNameKey => "Tool_MyNewUtility";

    // Иконка (из Fluent System Icons)
    public string IconGlyph => "\uE946"; // Пример: иконка для новой утилиты

    // Цвет иконки
    public string IconColor => "#007ACC";

    public async void Launch(AppSettingsService settingsService, Window? owner, Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null)
        {
            await onBeforeExecute();
        }

        // Если окно уже открыто — активируем его
        if (_window is { IsVisible: true })
        {
            _window.Activate();
            return;
        }

        // Создаём новое окно
        _window = new MyNewUtilityWindow
        {
            Owner = owner
        };

        // Очищаем ссылку при закрытии окна
        _window.Closed += (s, e) => _window = null;

        // Показываем окно (с анимацией или рядом с панелью)
        _window.Show();
    }
}
```

### 2. Создайте окно для утилиты

Создайте WPF-окно для новой утилиты (например, `MyNewUtilityWindow.xaml` и `MyNewUtilityWindow.xaml.cs`).

### 3. Зарегистрируйте утилиту в реестре

В файле `App.xaml.cs` добавьте регистрацию новой утилиты в методе `RegisterUtilities`:

```csharp
private void RegisterUtilities()
{
    // Существующие утилиты
    UtilityRegistry.Register(new QuickNoteUtility());
    UtilityRegistry.Register(new TimerStopwatchUtility());
    UtilityRegistry.Register(new ColorPickerUtility());
    UtilityRegistry.Register(new FileSorterUtility());

    // Новая утилита
    UtilityRegistry.Register(new MyNewUtility());
}
```

### 4. Добавьте локализованные строки

В файлы ресурсов (`Strings.resx`, `Strings.ru.resx` и т.д.) добавьте ключ для отображения имени утилиты в настройках:

| Ключ               | Значение (RU) |
|--------------------|---------------|
| Tool_MyNewUtility  | Моя утилита   |

### 5. Добавьте кнопку на панель

В файле `MainWindow.xaml` добавьте новую кнопку:

```xaml
<Button
    x:Name="BtnMyNewUtility"
    Click="BtnMyNewUtility_Click"
    Style="{StaticResource PanelActionButtonStyle}"
    ToolTip="{Loc Tool_MyNewUtility}">
    <TextBlock
        FontFamily="{StaticResource FluentSystemIcons}"
        Foreground="#007ACC"
        Text="\uE946" />
</Button>
```

В файле `MainWindow.xaml.cs` добавьте обработчик клика:

```csharp
private async void BtnMyNewUtility_Click(object sender, RoutedEventArgs e)
{
    await RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("MyNewUtility", HideDock));
}
```

### 6. (Опционально) Добавьте поддержку горячих клавиш

Чтобы новая утилита могла вызываться по горячей клавише, обновите:
1. Перечисление `HotkeyCommand` в `Models.cs`
2. Метод `HandleHotkeyPressed` в `HotkeyService.cs`
3. Окно настроек `SettingsWindow.xaml` и `SettingsWindow.xaml.cs`

## Примеры

Посмотрите реализацию существующих утилит для примера:
- `QuickNoteUtility.cs` (быстрые заметки)
- `TimerStopwatchUtility.cs` (таймер/секундомер)
- `ColorPickerUtility.cs` (пипетка для цвета)
- `FileSorterUtility.cs` (сортировщик файлов)
