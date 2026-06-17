# План: Безопасное разделение MainWindow

> 📖 **Инструкция по реализации**: [IMPLEMENT_MAINWINDOW_SPLIT.md](./IMPLEMENT_MAINWINDOW_SPLIT.md) — детальная пошаговая инструкция для выполнения этого плана.

## 1. Анализ текущего состояния

### MainWindow.xaml.cs — 2849 строк, 14 зон ответственности

| Зона | Строки | Методы |
|------|--------|--------|
| Show/Hide/Animation | ~300 | ShowDock, HideDock, Toggle, ToggleDock, PositionWindowImmediately, EnsureStartupInfrastructure |
| Layout & Orientation | ~200 | UpdateOrientation, ApplyPanelSizeConstraints, CalculateAvailableSize, ComputePanelMetrics, GetDockCoordinates |
| Context Management | ~100 | TryActivateContext, GetNextContextId, ActivateContextRelative, SwitchActiveContextAsync |
| Element CRUD | ~200 | DuplicateElementAsync, RenameElementAsync, DeleteElementAsync, MoveElementToContextAsync |
| Drag Reorder | ~250 | CreateUnifiedButton (drag handlers), CalculateUnifiedTargetIndex, UpdateUnifiedReorderPositions |
| Panel Drag (Edge) | ~100 | DragHandle_Mouse*, EndPanelDragAsync |
| Hotkeys | ~100 | RegisterGlobalHotkey, WndProc, ExecuteHotkeyCommand |
| Keyboard Nav | ~150 | Window_PreviewKeyDown, FocusPanelForKeyboard, SetPanelInputMode |
| Tray | ~50 | InitTrayIcon, ShowTrayContextMenu |
| Import/Export | ~100 | ExportCurrentPanelAsync, ImportIntoCurrentPanelAsync |
| File Drop | ~150 | Border_Drop, TryGetDropTarget |
| Button Clicks | ~50 | Btn*_Click handlers |
| Panel Refresh | ~200 | RefreshPanel, CreateUnifiedButton, ApplyUnifiedButtonIcon |
| Helpers | ~50 | RunPanelInteraction, RunPresetActionAsync, OpenUrl |

---

## 2. Стратегия разделения

### Принципы
1. **Partial class** — MainWindow остаётся одним классом, файлы разделяются по ответственности
2. **Без изменения поведения** — только перемещение методов, не логики
3. **Пошагово** — каждый шаг проверяется сборкой и тестами
4. **Откат** — каждый шаг можно откатить через git

### Порядок извлечения (от наименее зависимого к наиболее)

**Шаг 1: DragAndDropHandler** (~250 строк)
- Переместить: drag reorder логику из CreateUnifiedButton, CalculateUnifiedTargetIndex, UpdateUnifiedReorderPositions
- Зависимости: AppSettings, _unifiedButtons, _currentUnifiedButtons, _settingsService
- Файл: `AiteBar/DragAndDropHandler.cs`

**Шаг 2: KeyboardNavigationHandler** (~150 строк)
- Переместить: Window_PreviewKeyDown, FocusPanelForKeyboard, GetAllFocusableButtons, SetPanelInputMode, EnablePanelKeyboardMode
- Зависимости: _unifiedButtons, AppSettings
- Файл: `AiteBar/KeyboardNavigationHandler.cs`

**Шаг 3: PanelDragHandler** (~100 строк)
- Переместить: DragHandle_Mouse*, EndPanelDragAsync, SetDragHandleActive, SetPanelDragRenderingActive
- Зависимости: AppSettings, _settingsService
- Файл: `AiteBar/PanelDragHandler.cs`

**Шаг 4: TrayMenuHandler** (~50 строк)
- Переместить: InitTrayIcon, ShowTrayContextMenu
- Зависимости: _notifyIcon, AppSettings
- Файл: `AiteBar/TrayMenuHandler.cs`

**Шаг 5: DropHandler** (~150 строк)
- Переместить: Border_DragOver, Border_Drop, TryGetDropTarget, CanAcceptDropData
- Зависимости: _settingsService, AppSettings
- Файл: `AiteBar/DropHandler.cs`

**Шаг 6: ImportExportHandler** (~100 строк)
- Переместить: ExportCurrentPanelAsync, ImportIntoCurrentPanelAsync, BuildPanelPackageFileName
- Зависимости: _panelPackageService, _settingsService
- Файл: `AiteBar/ImportExportHandler.cs`

---

## 3. Детали реализации шага 1 (пример)

### Новый файл: `AiteBar/DragAndDropHandler.cs`
```csharp
namespace AiteBar;

// Partial class — та же partial MainWindow
public partial class MainWindow
{
    // CalculateUnifiedTargetIndex — перенести как есть
    private int CalculateUnifiedTargetIndex(Point currentPos) { ... }

    // UpdateUnifiedReorderPositions — перенести как есть
    private void UpdateUnifiedReorderPositions(Point currentPos) { ... }

    // ReorderUserElements — выделить из PreviewMouseUp
    private async Task ReorderUserElementsAsync(UnifiedButton draggedItem, int newIndex) { ... }

    // ReorderUtilityButtons — выделить из PreviewMouseUp
    private async Task ReorderUtilityButtonsAsync(UnifiedButton draggedItem, int newIndex) { ... }
}
```

### Изменения в `MainWindow.xaml.cs`
- Удалить перемещённые методы
- Оставить ссылки на новые partial-методы (они доступны автоматически)
- Добавить `using` если нужно

### Проверка
```powershell
dotnet build .\AiteBar.sln -c Release
dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
```

---

## 4. Сроки

| Шаг | Время | Риск |
|-----|-------|------|
| Шаг 1: DragAndDrop | 0.5 дня | Низкий |
| Шаг 2: KeyboardNav | 0.5 дня | Низкий |
| Шаг 3: PanelDrag | 0.5 дня | Низкий |
| Шаг 4: TrayMenu | 0.5 дня | Низкий |
| Шаг 5: DropHandler | 0.5 дня | Низкий |
| Шаг 6: ImportExport | 0.5 дня | Низкий |
| Тестирование | 1 день | Средний |
| **Итого** | **4 дня** | |

---

## 5. Критерии успеха

- ✅ Сборка проходит без ошибок
- ✅ Все тесты проходят
- ✅ MainWindow уменьшился до ~1500 строк
- ✅ 6 новых partial-файлов
- ✅ Поведение приложения не изменилось
- ✅ Все 4 стороны панели работают (Top/Bottom/Left/Right)
- ✅ Drag-and-drop работает
- ✅ Контексты переключаются
- ✅ Tray menu работает

---

## 6. Риски и митигация

| Риск | Вероятность | Митигация |
|------|-------------|-----------|
| Сломать поведение | Низкая | Partial class не меняет логику |
| Забыть using | Низкая | Компилятор покажет ошибку |
| WPF temp файлы залочены | Средняя | Закрыть IDE перед сборкой |

---

## 7. Необходимые условия

- IDE (Trae/Visual Studio) должны быть закрыты перед сборкой
- Все тесты должны проходить до начала изменений