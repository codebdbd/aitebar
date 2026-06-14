# AiteBar — Оставшиеся проблемы и безопасные исправления

> Дата: 2025-06-14
> Принцип: каждое исправление должно быть максимально безопасным, не менять поведение приложения, не ломать существующие функции.

---

## Содержание

1. [P1: God-class MainWindow — декомпозиция](#p1-god-class-mainwindow--декомпозиция)
2. [P5: Тройной вызов PanelLayoutHelper.Calculate](#p5-тройной-вызов-panellayouthelpercalculate)
3. [P10: ForceForegroundWindow —_AttachThreadInput](#p10-forceforegroundwindow--attachthreadinput)
4. [L1: Версия в трёх местах](#l1-версия-в-трёх-местах)
5. [L2: Logger блокирует UI-поток](#l2-logger-блокирует-ui-поток)
6. [L4: Нет CancellationToken в deferred startup](#l4-нет-cancellationtoken-в-deferred-startup)
7. [L5: Fire-and-forget без error handling](#l5-fire-and-forget-без-error-handling)
8. [N1: HttpClient не compartir](#n1-httpclient-не-compart)
9. [N2: Toggle() — замыкание на completedCount](#n2-toggle--замыкание-на-completedcount)
10. [N3: RefreshPanel пересоздаёт все кнопки](#n3-refreshpanel-пересоздаёт-все-кнопки)
11. [N4: _activeContextElements — общее состояние](#n4-_activecontextelements--общее-состояние)
12. [N5: DispatcherTimer 30ms без проверки visibility](#n5-dispatchertimer-30ms-без-проверки-visibility)
13. [N6: Логгер уязвим к Path Injection](#n6-логгер-уязвим-к-path-injection)
14. [N7: BrowserHelper.ReadPreference — файл может быть заблокирован](#n7-browserhelperreadpreference--файл-могут-быть-заблокирован)
15. [Итоговая таблица](#итоговая-таблица)

---

## P1: God-class MainWindow — декомпозиция

**Файл**: `AiteBar/MainWindow.xaml.cs` (2726 строк)
**Статус**: Не исправлено
**Приоритет**: Высокий
**Риск исправления**: Средний (рефакторинг большого файла)

### Что не так

Один класс содержит: UI панели, drag-and-drop, анимации, контекстные меню, глобальные hotkeys, tray-иконка, обработка колёсика мыши, позиционирование окна, управление контекстами, импорт/экспорт пакетов, favicon download. ~65 методов, ~30 приватных полей.

### Безопасное исправление — пошаговый Extract

**Шаг 1: Extract `PanelPositionHelper`** (безопасно, чистая математика)

Создать `AiteBar/PanelPositionHelper.cs` — статический класс без зависимостей от UI:

```csharp
namespace AiteBar;

internal static class PanelPositionHelper
{
    public static (double X, double Y) GetDockCoordinates(
        DockEdge edge,
        Rect workArea,
        Rect bounds,
        double panelWidth,
        double panelHeight,
        double topPanelVisibleOffset,
        bool hide)
    {
        double centeredX = workArea.Left + Math.Max(0, (workArea.Width - panelWidth) / 2);
        double centeredY = workArea.Top + Math.Max(0, (workArea.Height - panelHeight) / 2);

        return edge switch
        {
            DockEdge.Top => (centeredX, hide ? bounds.Top - panelHeight : workArea.Top + topPanelVisibleOffset),
            DockEdge.Bottom => (centeredX, hide ? bounds.Bottom : workArea.Bottom - panelHeight),
            DockEdge.Left => (hide ? bounds.Left - panelWidth : workArea.Left, centeredY),
            DockEdge.Right => (hide ? bounds.Right : workArea.Right - panelWidth, centeredY),
            _ => (workArea.Left, workArea.Top)
        };
    }

    public static DockEdge GetClosestDockEdge(Rect workArea, int cursorX, int cursorY, DockEdge currentEdge)
    {
        var distances = new Dictionary<DockEdge, int>
        {
            [DockEdge.Top] = Math.Abs(cursorY - workArea.Top),
            [DockEdge.Bottom] = Math.Abs(workArea.Bottom - cursorY),
            [DockEdge.Left] = Math.Abs(cursorX - workArea.Left),
            [DockEdge.Right] = Math.Abs(workArea.Right - cursorX)
        };
        distances[currentEdge] -= 60;
        return distances.OrderBy(pair => pair.Value).First().Key;
    }
}
```

В `MainWindow.xaml.cs` заменить методы `GetDockCoordinates` и `GetClosestDockEdge` на вызовы `PanelPositionHelper.*`.

**Шаг 2: Extract `ContextSwitchHelper`** (чистая логика, без UI)

```csharp
namespace AiteBar;

internal static class ContextSwitchHelper
{
    public static int FindNextContextIndex(
        IReadOnlyList<PanelContext> enabledContexts,
        string activeContextId,
        int direction)
    {
        int currentIndex = enabledContexts.ToList().FindIndex(
            c => string.Equals(c.Id, activeContextId, StringComparison.Ordinal));
        if (currentIndex < 0) currentIndex = 0;
        return ContextStateHelper.WrapIndex(currentIndex + direction, enabledContexts.Count);
    }
}
```

В MainWindow заменить дублирующийся код в `SwitchActiveContextAsync`, `ActivateContextRelative`, `ActivateContextByIndex`, `ActivateContextById` на вызов `ContextSwitchHelper.FindNextContextIndex`.

**Шаг 3: Extract `PanelRenderer`** (только в будущем, высокий риск)

Не делать сейчас. Это потребует выноса `RefreshPanel`, `ApplyPanelSizeConstraints`, `ApplySystemUtilityVisibility`, `BuildPanelContextMenu`, `BuildElementContextMenu` — все они напрямую обращаются к XAML-элементам. Без MVVM или DI это будет болезненно.

### Что НЕ делать

- Не выносить `Toggle`, `ShowDock`, `HideDock` — они привязаны к анимациям WPF.
- Не выносить `InitTrayIcon` — зависит от `NotifyIcon`.
- Не выносить `WndProc` — зависит от Win32 interop.

---

## P5: Тройной вызов PanelLayoutHelper.Calculate

**Файл**: `AiteBar/MainWindow.xaml.cs:703-822, 1548-1584`
**Статус**: Не исправлено
**Приоритет**: Средний
**Риск исправления**: Низкий

### Что не так

`PanelLayoutHelper.Calculate()` вызывается 3 раза за одно обновление панели:
1. `ApplyPanelSizeConstraints()` строка 749 — `tempMetrics`
2. `ApplyPanelSizeConstraints()` строка 764 — `metrics`
3. `RefreshPanel()` строка 1560 — `tempMetrics`

Параметры `availableWidth/Height` различаются:
- `ApplyPanelSizeConstraints`: `(workArea.Width / _cachedDpi) - PanelScreenPadding` (20)
- `RefreshPanel`: `(workArea.Width / _cachedDpi) - PanelLayoutHelper.PanelChrome` (8)

### Безопасное исправление

Вынести вычисление `availableWidth/availableHeight` в единый метод, и вызывать `Calculate()` только дважды (temp + final) в `RefreshPanel`, а `ApplyPanelSizeConstraints` принимать готовый `metrics`:

**Шаг 1**: Создать вспомогательный метод:

```csharp
private (double availableWidth, double availableHeight) CalculateAvailableSize()
{
    var screen = GetTargetScreen();
    var workArea = screen?.WorkingArea;
    double availableWidth = workArea.HasValue
        ? Math.Max(150, (workArea.Value.Width / _cachedDpi) - PanelScreenPadding)
        : 150;
    double availableHeight = workArea.HasValue
        ? Math.Max(150, (workArea.Value.Height / _cachedDpi) - PanelScreenPadding)
        : 150;
    return (availableWidth, availableHeight);
}
```

**Шаг 2**: Изменить `RefreshPanel()` — убрать дублирующий вызов:

```csharp
// В RefreshPanel() — убрать строки 1548-1573 и заменить на:
var (availableWidth, availableHeight) = CalculateAvailableSize();
var metrics = ComputePanelMetrics(isVertical, availableWidth, availableHeight);
ApplyPanelSizeConstraints(metrics);
// ... остальная часть RefreshPanel
```

**Шаг 3**: Изменить `ApplyPanelSizeConstraints()` — принимать готовый metrics:

```csharp
private void ApplyPanelSizeConstraints(PanelLayoutHelper.PanelLayoutMetrics metrics)
{
    bool isVertical = AppSettings.Edge == DockEdge.Left || AppSettings.Edge == DockEdge.Right;
    // ... сброс размеров (строки 709-734) — оставить как есть
    // Убрать вычисление metrics — он уже передан
    // Оставить только применение: RootBorder.MinWidth = Math.Round(metrics.PanelWidth); и т.д.
}
```

**Шаг 4**: Обновить вызов в `UpdateOrientation()`:

```csharp
// В UpdateOrientation() строка 1437:
var (availableWidth, availableHeight) = CalculateAvailableSize();
var metrics = ComputePanelMetrics(isVertical, availableWidth, availableHeight);
ApplyPanelSizeConstraints(metrics);
```

Где `ComputePanelMetrics` — новый метод, объединяющий temp+final расчёт:

```csharp
private PanelLayoutHelper.PanelLayoutMetrics ComputePanelMetrics(
    bool isVertical, double availableWidth, double availableHeight)
{
    int visibleSystemButtonCount = GetVisibleSystemButtonCount();
    var contextCountsList = ContextStateHelper.GetEnabledContexts(AppSettings.Contexts)
        .Select(context => Elements.Count(e => string.Equals(e.ContextId, context.Id, StringComparison.Ordinal)))
        .ToList();
    int activeContextIdx = Math.Max(0, ContextStateHelper.GetEnabledContexts(AppSettings.Contexts)
        .ToList().FindIndex(c => string.Equals(c.Id, AppSettings.ActiveContextId, StringComparison.Ordinal)));

    var tempMetrics = PanelLayoutHelper.Calculate(
        isVertical: isVertical,
        availablePrimary: isVertical ? availableHeight : availableWidth,
        panelPercent: AppSettings.PanelSizePercent,
        visibleSystemButtonCount: visibleSystemButtonCount,
        controlButtonCount: 1,
        contextCounts: contextCountsList,
        activeContextIndex: activeContextIdx,
        systemContextIndex: 0,
        trailingControlButtonCount: 1);

    bool hasUserButtons = contextCountsList.Any(c => c > 0);
    bool hideSepControl = isVertical && hasUserButtons && tempMetrics.UserBands == 2;

    return PanelLayoutHelper.Calculate(
        isVertical: isVertical,
        availablePrimary: isVertical ? availableHeight : availableWidth,
        panelPercent: AppSettings.PanelSizePercent,
        visibleSystemButtonCount: visibleSystemButtonCount,
        controlButtonCount: 1,
        contextCounts: contextCountsList,
        activeContextIndex: activeContextIdx,
        systemContextIndex: 0,
        trailingControlButtonCount: 1,
        hideControlSeparator: hideSepControl);
}
```

---

## P10: ForceForegroundWindow — AttachThreadInput

**Файл**: `AiteBar/MainWindow.xaml.cs:2046-2073`
**Статус**: Не исправлено
**Приоритет**: Низкий
**Риск исправления**: Средний (может сломать фокус)

### Что не так

`AttachThreadInput` + `SetForegroundWindow` может нарушить focus model других приложений.

### Безопасное исправление

**Не трогать**. Это стандартный Win32 паттерн для показа окна из фона. Альтернативы (`AllowSetForegroundWindow`, `FlashWindowEx`) менее надёжны и не дают того же эффекта. Windows 10/11 имеют дополнительные ограничения на `SetForegroundWindow`, и `AttachThreadInput` — единственный способ обойти их для собственного окна.

Единственное улучшение — добавить проверку `IsDisposed`:

```csharp
private static bool ForceForegroundWindow(IntPtr hwnd)
{
    if (hwnd == IntPtr.Zero) return false;
    // ... существующий код
}
```

Это уже сделано. Оставить как есть.

---

## L1: Версия в трёх местах

**Файлы**:
- `AiteBar/AiteBar.csproj:16` — `<Version>1.7.9</Version>`
- `AiteBar/AssemblyInfo.cs:11-13` — `AssemblyVersion`, `AssemblyFileVersion`, `AssemblyInformationalVersion`

**Приоритет**: Низкий
**Риск исправления**: Низкий

### Безопасное исправление

**Шаг 1**: Добавить в `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <Version>1.7.9</Version>
  </PropertyGroup>
</Project>
```

**Шаг 2**: Убрать `<Version>`, `<AssemblyVersion>`, `<FileVersion>` из `AiteBar.csproj`.

**Шаг 3**: Убрать `AssemblyVersion`, `AssemblyFileVersion` из `AssemblyInfo.cs`. Оставить только `AssemblyInformationalVersion` (если нужен informational version отличный от 3-part).

**Но**: Это может сломать `Assembly.GetExecutingAssembly().GetName().Version` в `TelemetryService.GetAppVersion()` и `UpdateCheckService.GetCurrentVersion()`. Проверить, что `Version` в `Directory.Build.props` автоматически генерирует `AssemblyVersion` и `FileVersion`.

**Альтернатива (более безопасная)**: Оставить как есть. Синхронизация — ручная, но проверяемая.

---

## L2: Logger блокирует UI-поток

**Файл**: `AiteBar/Logger.cs:15-37`
**Приоритет**: Низкий
**Риск исправления**: Низкий

### Что не так

`File.AppendAllText` синхронный, вызывается из UI-потока через `catch` блоки в MainWindow.

### Безопасное исправление

Добавить async-версию логирования и использовать её в async-контекстах:

```csharp
internal static class Logger
{
    // ... существующий код

    public static async Task LogAsync(Exception ex)
    {
        try
        {
            string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n";
            lock (_lockObj)
            {
                string? dir = Path.GetDirectoryName(LogPath);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogSizeBytes)
                    RotateLogFile();
            }
            await File.AppendAllTextAsync(LogPath, message);
        }
        catch (Exception logEx)
        {
            Debug.WriteLine(logEx);
        }
    }
}
```

В `MainWindow` заменить `Logger.Log(ex)` на `_ = Logger.LogAsync(ex)` в async-методах. Синхронные `Logger.Log(ex)` оставить как есть — они не блокируют критично.

---

## L4: Нет CancellationToken в deferred startup

**Файл**: `AiteBar/MainWindow.xaml.cs:1356-1377`
**Приоритет**: Средний
**Риск исправления**: Низкий

### Безопасное исправление

**Шаг 1**: Добавить поле в `MainWindow`:

```csharp
private CancellationTokenSource? _startupCts;
```

**Шаг 2**: Инициализировать в конструкторе `MainWindow`:

```csharp
private MainWindow(AppSettingsService settingsService, bool settingsPreloaded)
{
    // ... существующий код
    _startupCts = new CancellationTokenSource();
}
```

**Шаг 3**: Изменить `CompleteDeferredStartupAsync`:

```csharp
private async Task CompleteDeferredStartupAsync()
{
    if (_deferredStartupCompleted) return;

    var token = _startupCts?.Token ?? CancellationToken.None;
    try
    {
        await Task.Run(async () => await _settingsService.LoadAsync(), token);
        token.ThrowIfCancellationRequested();
        LocalizationService.ApplyCulture(AppSettings.UiCulture);
        _deferredStartupCompleted = true;
        ApplyLocalizedText();
        RegisterGlobalHotkey();
        RefreshPanel();
        PositionWindowImmediately(_shown);
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) { Logger.Log(ex); }
}
```

**Шаг 4**: Отменить в `OnClosed`:

```csharp
protected override void OnClosed(EventArgs e)
{
    try
    {
        _startupCts?.Cancel();
        _startupCts?.Dispose();
        // ... существующий код
    }
    finally { base.OnClosed(e); }
}
```

---

## L5: Fire-and-forget без error handling

**Файл**: `AiteBar/MainWindow.xaml.cs:1284`
**Приоритет**: Низкий
**Риск исправления**: Низкий

### Что не так

```csharp
_ = CompleteDeferredStartupAsync(); // строка 1284
```

Если `CompleteDeferredStartupAsync` бросит исключение outside `try/catch` — он будет проглочен.

### Безопасное исправление

Добавить continuation для логирования:

```csharp
_ = CompleteDeferredStartupAsync().ContinueWith(task =>
{
    if (task.Exception != null)
        Logger.Log(task.Exception);
}, TaskContinuationOptions.OnlyOnFaulted);
```

---

## N1: HttpClient не компартится

**Файл**: `AiteBar/IconHelper.cs:15`, `AiteBar/UpdateCheckService.cs:210-216`
**Приоритет**: Низкий
**Риск исправления**: Средний

### Что не так

- `IconHelper._httpClient` — `static readonly HttpClient`. Не проблема для одиночного использования, но может привести к socket exhaustion при частых загрузках favicon.
- `UpdateCheckService.CreateHttpClient()` — создаёт новый `HttpClient` при каждом вызове.

### Безопасное исправление

Для `UpdateCheckService` — кешировать `HttpClient`:

```csharp
private static readonly HttpClient _sharedHttpClient = CreateHttpClient();

public UpdateCheckService()
    : this(_sharedHttpClient, new ProcessStartDispatcher())
{
}
```

Для `IconHelper` — оставить как есть. Favicon загружаются редко, `static HttpClient` — стандартный паттерн.

---

## N2: Toggle() — замыкание на completedCount

**Файл**: `AiteBar/MainWindow.xaml.cs:2269-2327`
**Приоритет**: Низкий
**Риск исправления**: Низкий

### Что не так

```csharp
int completedCount = 0;
void onCompleted(object? s, EventArgs ev)
{
    completedCount++;
    if (completedCount == 2) { /* ... */ }
}
animX.Completed += onCompleted;
animY.Completed += onCompleted;
```

Если анимация X завершится дважды (редкий edge case с WPF), `completedCount` станет 3 и финализация не сработает.

### Безопасное исправление

```csharp
int completedCount = 0;
void onCompleted(object? s, EventArgs ev)
{
    if (Interlocked.Increment(ref completedCount) == 2)
    {
        // ... существующий код
    }
}
```

`Interlocked.Increment` атомарен и гарантирует, что блок выполнится ровно один раз при достижении 2.

---

## N3: RefreshPanel пересоздаёт все кнопки

**Файл**: `AiteBar/MainWindow.xaml.cs:1445-1585`
**Приоритет**: Средний
**Риск исправления**: Средний

### Что не так

`RefreshPanel()` делает `UserButtonsPanel.Children.Clear()` и пересоздаёт все кнопки заново. При 20+ кнопках это вызывает:
- Видимый flicker (мигание).
- Аллокации памяти на каждый refresh.
- Потерю состояния фокуса.

### Безопасное исправление

**Не делать сейчас**. Требует значительного рефакторинга: diffing текущих кнопок с новыми, обновление существующих вместо пересоздания. Риск сломать drag-and-drop非常高.

В будущем — замена на `ItemsControl` с `ObservableCollection` и `DataTemplate`.

---

## N4: _activeContextElements — общее состояние

**Файл**: `AiteBar/MainWindow.xaml.cs:106`
**Приоритет**: Низкий
**Риск исправления**: Низкий

### Что не так

```csharp
private List<CustomElement> _activeContextElements = [];
```

Это поле обновляется в `RefreshPanel()` и читается в `CalculateTargetIndex()`, `UpdateReorderPositions()`. Если `RefreshPanel` вызовется во время drag — состояние будет рассинхронизировано.

### Безопасное исправление

**Не трогать**. `RefreshPanel` уже блокируется через `_isBlockingPanelInteraction` и `_isPanelDragging`. Существующая защита достаточна.

---

## N5: DispatcherTimer 30ms без проверки visibility

**Файл**: `AiteBar/MainWindow.xaml.cs:50, 1309-1353`
**Приоритет**: Низкий
**Риск исправления**: Низкий

### Что не так

```csharp
private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
```

Тикер работает каждые 30ms (~33 раза в секунду) даже когда панель скрыта. Каждый тик вызывает `GetCursorPos` + `ActivationZoneHelper.IsInActivationZone`.

### Безопасное исправление

Остановить тикер при скрытии, запустить при показе:

```csharp
private void ShowDock(bool fromKeyboard = false)
{
    // ... существующий код
    _timer.Start(); // уже есть в Toggle
}

private async Task HideDock()
{
    // ... существующий код
    _timer.Stop(); // добавить после Toggle
}
```

Но: `_timer.Stop()` уже вызывается в `Toggle()` (строка 2271: `_timer.Stop()`), и `_timer.Start()` — в `onCompleted` (строка 2305). Проблема в том, что `EnsureStartupInfrastructure` (строка 1351) запускает тикер до первого показа.

**Более точечное исправление**: В `EnsureStartupInfrastructure` не запускать тикер сразу, а запускать его при первом `ShowDock`:

```csharp
// Убрать _timer.Start() из EnsureStartupInfrastructure (строка 1351)
// Добавить в ShowDock():
if (!_timer.IsEnabled) _timer.Start();
```

---

## N6: Logger уязвим к Path Injection

**Файл**: `AiteBar/Logger.cs:30`
**Приоритет**: Очень низкий
**Риск исправления**: Trivial

### Что не так

```csharp
File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
```

Если `ex.Message` содержит `\n` — следующая строка лога будет вставлена в середину записи. Не критично для логов, но может сломать парсинг.

### Безопасное исправление

Экранировать newlines в сообщении:

```csharp
string safeMessage = ex.ToString().Replace("\r\n", "\n").Replace("\n", " | ");
File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {safeMessage}\n\n");
```

---

## N7: BrowserHelper.GetProfiles — файл может быть заблокирован

**Файл**: `AiteBar/BrowserHelper.cs:158-183`
**Приоритет**: Очень низкий
**Риск исправления**: Trivial

### Что не так

```csharp
using var stream = File.OpenRead(prefFile);
using var doc = JsonDocument.Parse(stream);
```

Если браузер открыт и записывает в `Preferences` — `File.OpenRead` может бросить `IOException`.

### Безопасное исправление

Добавить `FileShare.ReadWrite`:

```csharp
using var stream = File.Open(prefFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
using var doc = JsonDocument.Parse(stream);
```

---

## Итоговая таблица

| ID | Проблема | Приоритет | Риск fix | Рекомендация |
|---|---|---|---|---|
| P1 | God-class MainWindow | Высокий | Средний | Extract PanelPositionHelper + ContextSwitchHelper |
| P5 | Тройной Calculate() | Средний | Низкий | Unified ComputePanelMetrics() |
| P10 | ForceForegroundWindow | Низкий | Средний | Оставить как есть |
| L1 | Версия в 3 местах | Низкий | Низкий | Directory.Build.props (или оставить) |
| L2 | Logger блокирует UI | Низкий | Низкий | Добавить LogAsync() |
| L4 | Нет CancellationToken | Средний | Низкий | CancellationTokenSource в MainWindow |
| L5 | Fire-and-forget | Низкий | Низкий | ContinueWith + OnlyOnFaulted |
| N1 | HttpClient не компартится | Низкий | Средний | Кешировать в UpdateCheckService |
| N2 | Toggle completedCount | Низкий | Низкий | Interlocked.Increment |
| N3 | RefreshPanel пересоздаёт кнопки | Средний | Высокий | Не делать сейчас |
| N4 | _activeContextElements | Низкий | Низкий | Оставить (защищено) |
| N5 | Timer 30ms без visibility | Низкий | Низкий | Не запускать до первого ShowDock |
| N6 | Logger Path Injection | Очень низкий | Trivial | Replace newlines |
| N7 | BrowserHelper FileShare | Очень низкий | Trivial | FileShare.ReadWrite |

### Рекомендуемый порядок исправлений

1. **L4** — CancellationToken (простое, полезное)
2. **L5** — Fire-and-forget error handling (простое)
3. **N2** — Interlocked.Increment (trivial)
4. **N7** — FileShare.ReadWrite (trivial)
5. **N6** — Logger newlines (trivial)
6. **L2** — Logger async (низкий риск)
7. **P5** — Unified layout calculation (средний риск, требует тестирования)
8. **N5** — Timer visibility (низкий риск)
9. **N1** — HttpClient caching (средний риск)
10. **P1** — MainWindow extract (высокий риск, делать последним)
