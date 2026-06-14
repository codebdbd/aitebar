# План: Единообразное поведение кнопок утилит и пользовательских кнопок

> Дата: 2025-06-14
> Цель: Кнопки утилит (Search, Screenshot, etc.) должны вести себя как пользовательские кнопки — поддерживать drag-and-drop reorder и overflow wrap на 2 ряда.

---

## Текущая проблема

### Как сейчас устроены кнопки утилит

```
XAML:
  SystemUtilsPanel (OverflowWrapPanel)
    ├── BtnSearch
    ├── BtnScreenshot
    ├── BtnRecord
    ├── ... (11 кнопок)
    └── BtnQuickNote
```

**PanelLayoutHelper** treats system buttons as **fixed single-column**:
```csharp
// PanelLayoutHelper.cs:304-309
private static UserLayout CalculateSingleColumnRows(int buttonCount)
{
    return normalizedCount == 0
        ? new UserLayout(0, 0, 0)
        : new UserLayout(normalizedCount * ButtonOuterSize, ButtonOuterSize, 1);
}
```

Результат: при 11+ кнопках утилит в вертикальном режиме — все в одну колонку, без overflow wrap.

### Как сейчас устроены пользовательские кнопки

```
UserButtonsPanel (OverflowWrapPanel)
  ├── [user button 1]
  ├── [user button 2]
  └── ... (динамические)
```

**PanelLayoutHelper** treats user buttons with **2-band overflow**:
```csharp
// PanelLayoutHelper.cs:265-281
public static UserLayout CalculateUserLayout(int buttonCount, double userPrimaryLimit)
{
    int maxItemsPerBand = (int)Math.Floor(userPrimaryLimit / ButtonOuterSize);
    int requiredBands = (int)Math.Ceiling(count / (double)maxItemsPerBand);
    int bands = Math.Min(MaxUserBands, requiredBands); // MaxUserBands = 2
    // ...
}
```

### Разница в поведении

| Аспект | Утилиты | Пользовательские |
|---|---|---|
| Overflow | Одна колонка (fixed) | 2 ряда (wrap) |
| Reorder | Нет | Drag-and-drop |
| Позиция в layout | `FixedLayout` → `System` | `UserLayout` → `User` |
| Drag handle | Нет | PreviewMouseDown/Move/Up |

---

## Решение: Unified Button Model

### Концепция

Объединить кнопки утилит и пользовательских в **единую модель данных** — `UnifiedButton`. Каждая кнопка имеет:
- `Type` (Utility / User)
- `Id` (для утилит — фиксированный, для пользовательских — GUID)
- `Order` (порядок в панели)
- `IsVisible` (для утилит — зависит от `ShowPreset*`, для пользовательских — всегда видна)

### Архитектура

```
┌─────────────────────────────────────────────┐
│              UnifiedButtonPanel              │
│              (OverflowWrapPanel)             │
│                                             │
│  [Search] [Screenshot] [Calc] [user1] [u2]  │  ← единый поток
│  [Explorer] [user3] [user4] [QuickNote]     │  ← overflow wrap
│                                             │
└─────────────────────────────────────────────┘
```

Вместо двух `OverflowWrapPanel` — один. Вместо `FixedLayout + UserLayout` — единый `UserLayout` для всех кнопок.

---

## Пошаговый план

### Фаза 1: Модель данных (безопасно, без UI)

#### Шаг 1.1: Добавить `UnifiedButton` модель

**Файл**: новый `AiteBar/UnifiedButton.cs`

```csharp
namespace AiteBar;

public sealed class UnifiedButton
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string IconFont { get; set; } = FontHelper.FluentKey;
    public string Color { get; set; } = "#E3E3E3";
    public string ImagePath { get; set; } = "";
    public UnifiedButtonType Type { get; set; }
    public int Order { get; set; }
    public bool IsVisible { get; set; } = true;

    // Для утилит
    public string? SettingsKey { get; set; } // "ShowPresetSearch" и т.д.

    // Для пользовательских кнопок
    public CustomElement? SourceElement { get; set; }
}

public enum UnifiedButtonType
{
    Utility,
    User
}
```

#### Шаг 1.2: Добавить `UnifiedButtonService`

**Файл**: новый `AiteBar/UnifiedButtonService.cs`

Отвечает за:
- Построение unified list из утилит + пользовательских кнопок
- Сохранение порядка (для утилит — в `AppSettings`, для пользовательских — через `_settingsService.ReorderElements`)
- Управление видимостью утилит

```csharp
namespace AiteBar;

internal sealed class UnifiedButtonService
{
    private readonly AppSettingsService _settingsService;

    public UnifiedButtonService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public List<UnifiedButton> BuildUnifiedList(string activeContextId)
    {
        var result = new List<UnifiedButton>();

        // 1. Утилиты (в порядке из AppSettings)
        AddUtilityIfVisible(result, "Search", "Main_SearchTooltip", "\uEA7C", "#3ABEFF", AppSettings.ShowPresetSearch, 0);
        AddUtilityIfVisible(result, "Screenshot", "Main_ScreenshotTooltip", "\uF68E", "#60A5FA", AppSettings.ShowPresetScreenshot, 1);
        // ... все 11 утилит

        // 2. Пользовательские кнопки
        var userElements = _settingsService.Elements
            .Where(e => e.ContextId == activeContextId)
            .ToList();
        foreach (var el in userElements)
        {
            result.Add(new UnifiedButton
            {
                Id = el.Id,
                Name = el.Name,
                Icon = el.Icon,
                IconFont = el.IconFont,
                Color = el.Color,
                ImagePath = el.ImagePath,
                Type = UnifiedButtonType.User,
                Order = result.Count,
                SourceElement = el
            });
        }

        return result;
    }

    public void Reorder(int oldIndex, int newIndex, string activeContextId)
    {
        // Для утилит — обновить порядок в AppSettings
        // Для пользовательских — _settingsService.ReorderElements
        // Для смешанного — определить тип по oldIndex/newIndex
    }
}
```

---

### Фаза 2: UI рефакторинг (средний риск)

#### Шаг 2.1: Заменить XAML кнопки утилит на единый ItemsControl

**Файл**: `AiteBar/MainWindow.xaml`

Заменить:
```xml
<local:OverflowWrapPanel x:Name="SystemUtilsPanel" Orientation="Horizontal">
    <Button x:Name="BtnSearch" ... />
    <Button x:Name="BtnScreenshot" ... />
    <!-- 11 кнопок -->
</local:OverflowWrapPanel>
```

На:
```xml
<local:OverflowWrapPanel x:Name="UnifiedButtonsPanel" Orientation="Horizontal"
                          ClipToBounds="True" VerticalAlignment="Center"/>
```

#### Шаг 2.2: Вынести определения кнопок утилит из XAML в код

**Файл**: `AiteBar/MainWindow.xaml.cs`

Создать статический каталог утилит:
```csharp
private static class UtilityButtonCatalog
{
    public static readonly IReadOnlyList<UtilityButtonDef> All =
    [
        new("Search", "\uEA7C", "#3ABEFF", "ShowPresetSearch"),
        new("Screenshot", "\uF68E", "#60A5FA", "ShowPresetScreenshot"),
        new("Record", "\uF535", "#FB7185", "ShowPresetVideo"),
        new("Calc", "\uF06C", "#A3E635", "ShowPresetCalc"),
        new("Explorer", "\uF42F", "#F59E0B", "ShowPresetExplorer"),
        new("Downloads", "\uF151", "#34D399", "ShowPresetDownloads"),
        new("FileSorter", "\uF202", "#60A5FA", "ShowPresetFileSorter"),
        new("IconConverter", "\uF12F", "#2DD4BF", "ShowPresetIconConverter"),
        new("TimerStopwatch", "\uED88", "#38BDF8", "ShowPresetTimerStopwatch"),
        new("ColorPicker", "\uE5FE", "#A855F7", "ShowPresetColorPicker"),
        new("QuickNote", "\uF56F", "#22D3EE", "ShowPresetQuickNote"),
    ];
}

public record UtilityButtonDef(string Id, string Icon, string Color, string SettingsKey);
```

#### Шаг 2.3: Переписать `RefreshPanel` для unified модели

**Файл**: `AiteBar/MainWindow.xaml.cs`

```csharp
public void RefreshPanel()
{
    int panelVersion = unchecked(++_panelRefreshVersion);
    _settingsService.NormalizeAppState();
    BuildPanelContextMenu();
    string activeContextId = AppSettings.ActiveContextId;

    UpdateOrientation(reposition: false, applySizeConstraints: false);
    UnifiedButtonsPanel.Children.Clear();
    _unifiedButtons.Clear();

    // Единый список: утилиты + пользовательские
    var unifiedList = _unifiedButtonService.BuildUnifiedList(activeContextId);

    foreach (var item in unifiedList)
    {
        var btn = CreateUnifiedButton(item, panelVersion);
        UnifiedButtonsPanel.Children.Add(btn);
        _unifiedButtons.Add(btn);
    }

    // Layout через PanelLayoutHelper (единый UserLayout для всех)
    bool isVertical = AppSettings.Edge == DockEdge.Left || AppSettings.Edge == DockEdge.Right;
    var (availableWidth, availableHeight) = CalculateAvailableSize();
    var metrics = ComputePanelMetrics(isVertical, availableWidth, availableHeight);
    ApplyPanelSizeConstraints(metrics);

    // ... остальное
}
```

#### Шаг 2.4: Обновить `PanelLayoutHelper` для unified модели

**Файл**: `AiteBar/PanelLayoutHelper.cs`

Изменить `Calculate` — передавать **общее количество** кнопок вместо разделения на system/user:

```csharp
public static PanelLayoutMetrics Calculate(
    bool isVertical,
    double availablePrimary,
    double panelPercent,
    int totalButtonCount,      // ВМЕСТО visibleSystemButtonCount + contextCounts
    int controlButtonCount,
    int trailingControlButtonCount,
    bool hasTrailingControls,
    bool hideControlSeparator = false)
{
    // ... единый UserLayout для totalButtonCount
}
```

Убрать `CalculateFixedVerticalLayout` и `CalculateSingleColumnRows` — они больше не нужны.

---

### Фаза 3: Drag-and-drop для unified кнопок (средний риск)

#### Шаг 3.1: Добавить drag handlers на unified кнопки

**Файл**: `AiteBar/MainWindow.xaml.cs`

Добавить в `CreateUnifiedButton`:

```csharp
private Button CreateUnifiedButton(UnifiedButton item, int panelVersion)
{
    var btn = CreatePanelButton(string.Empty, item.Name, async (s, e) =>
    {
        if (ReferenceEquals(s, _suppressUserButtonClickFor))
        {
            _suppressUserButtonClickFor = null;
            return;
        }
        await ExecuteUnifiedButtonActionAsync(item);
    }, (Brush)_brushConverter.ConvertFromString(item.Color)!);

    btn.RenderTransform = new TranslateTransform();
    btn.Tag = item.Id;

    // Drag-and-drop handlers (единые для всех)
    btn.PreviewMouseDown += (s, e) => OnUnifiedButtonMouseDown(s, e, item);
    btn.PreviewMouseMove += (s, e) => OnUnifiedButtonMouseMove(s, e);
    btn.PreviewMouseUp += async (s, e) => await OnUnifiedButtonMouseUp(s, e, item);

    // Context menu
    btn.MouseRightButtonUp += (s, e) =>
    {
        btn.ContextMenu = BuildUnifiedButtonContextMenu(item);
    };

    ApplyButtonIcon(btn, item, panelVersion);
    return btn;
}
```

#### Шаг 3.2: Единая обработка context menu

**Файл**: `AiteBar/MainWindow.xaml.cs`

```csharp
private ContextMenu BuildUnifiedButtonContextMenu(UnifiedButton item)
{
    if (item.Type == UnifiedButtonType.Utility)
    {
        // Для утилит: Unpin (скрыть) + Edit (если поддерживается)
        return BuildUtilityContextMenu(item);
    }
    else
    {
        // Для пользовательских: Edit, Duplicate, Rename, Move, Delete
        return BuildElementContextMenu(item.SourceElement!);
    }
}
```

---

### Фаза 4: Удаление старого кода (низкий риск)

#### Шаг 4.1: Удалить XAML кнопки утилит

Удалить из `MainWindow.xaml`:
- `SystemUtilsPanel` (OverflowWrapPanel)
- `BtnSearch`, `BtnScreenshot`, `BtnRecord`, `BtnCalc`, `BtnExplorer`, `BtnDownloads`, `BtnFileSorter`, `BtnIconConverter`, `BtnTimerStopwatch`, `BtnColorPicker`, `BtnQuickNote`
- `SepSystem`, `SepControl` (разделители больше не нужны — единая панель)

#### Шаг 4.2: Удалить старые методы

Удалить из `MainWindow.xaml.cs`:
- `ApplySystemUtilityVisibility()` — заменено на unified model
- `AttachSystemUtilityContextMenus()` — заменено на unified context menu
- `BuildSystemUtilityContextMenu()` — заменено
- `GetVisibleSystemButtonCount()` — заменено на `UnifiedButtonsPanel.Children.Count`
- Все `Btn*_Click` handlers (11 штук) — заменены на единый `ExecuteUnifiedButtonActionAsync`

#### Шаг 4.3: Обновить `EnumeratePanelButtons`

```csharp
private IEnumerable<Button> EnumeratePanelButtons()
{
    yield return BtnAdd;
    foreach (var button in _unifiedButtons) yield return button;
    yield return BtnAppSettings;
}
```

---

## Риски и mitigations

| Риск | Вероятность | Mitigation |
|---|---|---|
| Поломка layout при переключении сторон | Средняя | Тестировать все 4 стороны до/после |
| Поломка drag-and-drop reorder | Средняя | Существующий код reorder переносится 1:1 |
| Поломка context menu утилит | Низкая | Unpin логика переносится в unified model |
| Поломка hotkeys | Низкая | Hotkeys не зависят от UI кнопок |
| Поломка visibility toggle | Низкая | `ShowPreset*` по-прежнему работает через `UnifiedButton.IsVisible` |

---

## Порядок проверки после реализации

1. Вертикальная панель (Left/Right) с 0 кнопками
2. Вертикальная панель с 5 утилитами — все в одну колонку
3. Вертикальная панель с 15 утилитами — overflow на 2 колонки
4. Вертикальная панель с 5 утилитами + 10 пользовательскими — mixed overflow
5. Горизонтальная панель (Top/Bottom) — аналогичные сценарии
6. Drag-and-drop reorder утилит
7. Drag-and-drop reorder смешанный (утилита + пользовательская)
8. Unpin утилиты через context menu
9. Показ/скрытие панели
10. Смена стороны через drag handle
11. Смена монитора
12. Переключение контекстов

---

## Оценка

| Фаза | Время | Сложность |
|---|---|---|
| Фаза 1: Модель данных | 2-3 часа | Low |
| Фаза 2: UI рефакторинг | 4-6 часов | Medium |
| Фаза 3: Drag-and-drop | 2-3 часа | Medium |
| Фаза 4: Удаление старого | 1-2 часа | Low |
| Тестирование | 2-3 часа | — |
| **Итого** | **11-17 часов** | **Medium** |
