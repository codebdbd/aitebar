# AiteBar — Отчёт выявленных проблем

> Дата анализа: 2025-06-14
> Версия приложения: 1.7.9
> Метод: ручной аудит кодовой базы

---

## Содержание

1. [Критичные проблемы](#1-критичные-проблемы)
2. [Проблемы среднейseverity](#2-проблемы-средней-серьёзности)
3. [Низкоприоритетные замечания](#3-низкоприоритетные-замечания)
4. [Рекомендации по решению](#4-рекомендации-по-решению)
5. [План действий](#5-план-действий)

---

## 1. Критичные проблемы

### P1: God-class — `MainWindow.xaml.cs` (2666 строк)

**Файл**: `AiteBar/MainWindow.xaml.cs`
**Причина**: Весь логический слой UI-панели, drag-and-drop, анимации, контекстные меню, глобальные hotkeys, tray-иконка, обработка колёсика мыши, позиционирование окна и управление контекстами сосредоточены в одном классе.

**Риски**:
- Любое изменение потенциально ломает смежную логику.
- Невозможно покрыть unit-тестами бизнес-логику (layout calculation, context switching, hotkey mapping) без WPF-контекста.
- Сложность code review и onboarding.

**Текущее состояние**: Класс содержит ~30+ приватных полей, ~60+ методов, обрабатывает события из XAML, Win32 WndProc, DispatcherTimer, и Background tasks.

---

### P2: Небезопасное выполнение команд и скриптов

**Файлы**:
- `AiteBar/ActionService.cs:262-272` — `ExecuteCommand()`
- `AiteBar/ActionService.cs:333-399` — `StartScriptFileAsync()`

**Проблема**: Пользовательские команды и скрипты выполняются через `cmd.exe /c` и `powershell.exe -ExecutionPolicy Bypass` без какого-либо sandboxing.

**Пример риска**:
```
Пользователь создаёт кнопку с командой:
calc.exe && del /q %APPDATA%\Codebdbd\Aite Bar\settings.json
```
Подтверждение показывается, но при подтверждении — команда выполняется целиком.

**Связанные риски**: Path injection при спецсимволах в путях к скриптам (пробелы, кавычки, `&`, `|`).

---

### P3: Глобальный mouse hook без fail-safe

**Файл**: `AiteBar/NativeIntegrationService.cs:22-36`

**Проблема**: `SetWindowsHookEx(WH_MOUSE_LL, ...)` устанавливает глобальный low-level hook. Если `Dispose()` не будет вызван (например, из-за необработанного исключения в `MainWindow.OnClosed`), hook останется активным до завершения процесса.

**Риски**:
- Message loop других приложений может блокироваться.
- На Windows 10/11 при завершении через Task Manager hook останется висеть до полного завершения процесса.

---

### P4: Блокирующий Flush в TelemetryService

**Файл**: `AiteBar/TelemetryService.cs:175`

**Проблема**: `SentrySdk.FlushAsync(timeout).GetAwaiter().GetResult()` — синхронная блокировка в потоке UI при `OnExit`.

**Риски**:
- Если Sentry-сервер недоступен, `OnExit` зависнет на 2 секунды.
- При определённых состояниях message loop — потенциальный deadlock.

---

## 2. Проблемы средней серьёзности

### P5: Дублирование layout-расчётов

**Файлы**:
- `AiteBar/MainWindow.xaml.cs:705-824` — `ApplyPanelSizeConstraints()`
- `AiteBar/MainWindow.xaml.cs:1550-1583` — `RefreshPanel()` (часть layout)

**Проблема**: `PanelLayoutHelper.Calculate()` вызывается дважды с разными параметрами:
- `ApplyPanelSizeConstraints()` — с `contextCountsList`, `activeContextIdx`
- `RefreshPanel()` — с теми же параметрами, но khácается `availableWidth/Height` (используется `PanelChrome` вместо `PanelScreenPadding`).

**Результат**: Потенциальное рассинхронизация размеров панели и её содержимого.

---

### P6: Дублирование констант анимаций

**Файлы**:
- `AiteBar/Constants.cs:16-17` — `PanelShowAnimationMs = 175`, `PanelHideAnimationMs = 140`
- `AiteBar/MainWindow.xaml.cs:123-124` — `private const int PanelShowAnimationMs = 175`, `private const int PanelHideAnimationMs = 140`

**Проблема**: Одни и те же значения определены в двух местах. `MainWindow` использует `Constants.*` в `Toggle()` (строка 2291), но также объявляет свои `const` поля. В `HideDock()` используется `PanelHideAnimationMs` (строка 2347) — но это **локальная** константа класса, а не `Constants`.

**Риски**: Если обновить `Constants`, локальные константы в `MainWindow` не обновятся.

---

### P7: Race condition в favicon download

**Файл**: `AiteBar/MainWindow.xaml.cs:2624-2646`

**Проблема**: При drag-and-drop веб-URL запускается `Task.Run` для скачивания favicon. После скачивания — `Dispatcher.InvokeAsync` обновляет элемент. Если пользователь удалит/переименует элемент до завершения download — `Elements.FirstOrDefault(x => x.Id == newElement.Id)` вернёт `null`, что безопасно, но если элемент будет переиспользован — обновится неверный `ImagePath`.

---

### P8: Fire-and-forget таймер в CaptureMouseForWheel

**Файл**: `AiteBar/MainWindow.xaml.cs:2370-2383`

**Проблема**: `Task.Delay(500).ContinueWith(t => Dispatcher.Invoke(...))` — не отслеживается. При закрытии окна `Dispatcher.Invoke` может бросить `ObjectDisposedException`.

---

### P9: Backup-ротация перед записью

**Файл**: `AiteBar/AppSettingsService.cs:189-215`

**Проблема**: `RotateBackups()` вызывается **до** `File.WriteAllTextAsync()`. Если запись упадёт после ротации — текущий `settings.json` удалён, бэкап `settings.json.backup.0` содержит предыдущую версию, а новая версия не записана.

**Результат**: Потеря изменений пользователя при ошибке записи (переполнение диска, antivirus lock).

---

### P10: Принудительное переключение foreground window

**Файл**: `AiteBar/MainWindow.xaml.cs:2048-2075`

**Проблема**: `ForceForegroundWindow()` использует `AttachThreadInput` + `SetForegroundWindow`. Это может нарушить focus model других приложений, особенно если AiteBar вызывается из фонового потока.

---

### P11: Связь MainWindow с ActionService напрямую

**Файлы**: `MainWindow.xaml.cs` (множественные вызовы `_actionService.*`)

**Проблема**: `MainWindow` напрямую вызывает ~10 методов `ActionService`, передавая `HideDock` callback. Нет промежуточного уровня абстракции. Тестирование `MainWindow` невозможно без мока всех этих зависимостей.

---

## 3. Низкоприоритетные замечания

### L1: Версия в трёх местах

**Файлы**:
- `AiteBar/AiteBar.csproj:16` — `<Version>1.7.9</Version>`
- `AiteBar/AssemblyInfo.cs:11-13` — `AssemblyVersion`, `AssemblyFileVersion`, `AssemblyInformationalVersion`

**Проблема**: При релизе нужно вручную синхронизировать все три файла. Ошибка приведёт к mismatched version в binary и installer.

---

### L2: Логгер блокирует UI-поток

**Файл**: `AiteBar/Logger.cs:16-37`

**Проблема**: `File.AppendAllText` внутри `lock` вызывается из UI-потока (через `catch` блоки). При частых ошибках на медленном диске — micro-freeze.

---

### L3: `using` aliases в MainWindow.xaml.cs

**Файл**: `AiteBar/MainWindow.xaml.cs:20-37`

**Проблема**: 18 строк `using` aliases для разрешения неоднозначности WPF vs WinForms. Это следствие одновременного использования `UseWPF=true` + `UseWindowsForms=true`. Не баг, но увеличивает cognitive load.

---

### L4: Нет cancellation token в long-running operations

**Файл**: `AiteBar/MainWindow.xaml.cs:1358` — `CompleteDeferredStartupAsync()`

**Проблема**: `await Task.Run(async () => await _settingsService.LoadAsync())` не имеет CancellationToken. Если настройки повреждены и `LoadAsync` зависнет — приложение будет висеть.

---

### L5: Fire-and-forget без error handling в Background задачах

**Файл**: `AiteBar/MainWindow.xaml.cs:2626`

**Проблема**: `_ = Task.Run(async () => { ... })` — fire-and-forget. Если внутри произойдёт unhandled exception в `async` лямбде, он будет молча проглочен (нет `await`).

---

## 4. Рекомендации по решению

### По P1: Разбиение MainWindow

**Подход**: Extract и вынесение логики в отдельные классы по зонам ответственности.

| Ответственность | Новый класс | Методы для переноса |
|---|---|---|
| Позиционирование и анимация | `PanelPositionService` | `GetDockCoordinates`, `PositionWindowImmediately`, `Toggle`, `AnimateContextTransitionIfNeeded` |
| Drag & Drop reorder | `PanelDragDropHandler` | `CalculateTargetIndex`, `UpdateReorderPositions`, все `_dragged*` поля |
| Panel rendering | `PanelRenderer` | `RefreshPanel`, `ApplyPanelSizeConstraints`, `ApplySystemUtilityVisibility`, `BuildPanelContextMenu`, `BuildElementContextMenu` |
| Context switching | `ContextSwitchService` | `SwitchActiveContextAsync`, `ActivateContextRelative`, `ActivateContextByIndex`, `ActivateContextById` |
| Hotkey routing | оставить в `MainWindow` | `WndProc`, `ExecuteHotkeyCommand` — минимальный остаток |

**Порядок**: Начинать с `PanelPositionService` (наименее связан с UI), затем `ContextSwitchService`, затем `PanelRenderer`.

---

### По P2: Sandbox для команд

**Рекомендации**:
1. Ограничить список разрешённых команд白名单 (calc, explorer, ms-screenclip и т.д.) для системных кнопок.
2. Для пользовательских `ActionType.Command` — показывать полный текст команды в диалоге подтверждения с выделением потенциально опасных операций (`del`, `rm`, `format`, `shutdown`).
3. Для скриптов — валидировать расширение и путь перед запуском.

---

### По P3: Fail-safe для mouse hook

**Рекомендации**:
1. Добавить `try/finally` в `MainWindow.OnClosed` для гарантированного вызова `_nativeService?.Dispose()`.
2. Рассмотреть `SafeHandle` или `IAsyncDisposable` для `NativeIntegrationService`.
3. Добавить fallback: если hook не установлен — игнорировать (сейчас уже есть `try/catch` в `InstallMouseHook`).

---

### По P4: Async Flush

**Рекомендация**: Заменить блокирующий вызов на fire-and-forget с таймаутом:
```csharp
_ = SentrySdk.FlushAsync(timeout).ContinueWith(t => 
    Logger.Log(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
```

---

### По P5-P6: Устранение дублирования

**Рекомендации**:
1. Убрать дублирующие `const` из `MainWindow.xaml.cs:123-124`. Использовать только `Constants.*`.
2. Вынести единый вызов `PanelLayoutHelper.Calculate()` в `RefreshPanel()` и передавать результат в `ApplyPanelSizeConstraints()`.

---

### По P9: Безопасная запись настроек

**Рекомендация**: Записывать во временный файл, затем атомарно перемещать:
```csharp
string tempFile = _settingsFile + ".tmp";
await File.WriteAllTextAsync(tempFile, json);
File.Move(tempFile, _settingsFile, overwrite: true);
RotateBackups(); // уже не критично
```

---

### По L1: Версионирование

**Рекомендация**: Централизовать версию через `Directory.Build.props`:
```xml
<PropertyGroup>
  <Version>1.7.9</Version>
</PropertyGroup>
```
И удалить `<Version>` из `.csproj`. Значения `AssemblyVersion` и `FileVersion` генерируются автоматически.

---

## 5. План действий

### Фаза 1: Быстрые победы (1-2 дня)

| # | Задача | Файл(ы) | Сложность |
|---|---|---|---|
| 1.1 | Убрать дублирующие const из MainWindow | `MainWindow.xaml.cs:123-124` | Trivial |
| 1.2 | Заменить блокирующий Flush на async | `TelemetryService.cs:175` | Low |
| 1.3 | Добавить try/finally для mouse hook disposal | `MainWindow.xaml.cs:2651-2663` | Low |
| 1.4 | Исправить backup-ротацию (запись → rename) | `AppSettingsService.cs:189-215` | Medium |
| 1.5 | Добавить CancellationToken в deferred startup | `MainWindow.xaml.cs:1358` | Low |

### Фаза 2: Безопасность (2-3 дня)

| # | Задача | Файл(ы) | Сложность |
|---|---|---|---|
| 2.1 | Добавить предупреждения о dangerous-командах в диалоге | `ActionService.cs`, `DarkDialog` | Medium |
| 2.2 | Валидация путей к скриптам (спецсимволы, traversal) | `ActionService.cs:333-399` | Medium |
| 2.3 | Защита от ObjectDisposedException в CaptureMouseForWheel | `MainWindow.xaml.cs:2370-2383` | Low |

### Фаза 3: Рефакторинг MainWindow (5-7 дней)

| # | Задача | Описание | Сложность |
|---|---|---|---|
| 3.1 | Extract `PanelPositionService` | Вынести позиционирование и анимацию | Medium |
| 3.2 | Extract `ContextSwitchService` | Вынести логику переключения контекстов | Medium |
| 3.3 | Extract `PanelRenderer` | Вынести `RefreshPanel`, `ApplyPanelSizeConstraints` | High |
| 3.4 | Unify layout calculation | Один вызов `PanelLayoutHelper.Calculate()` в `RefreshPanel`, результат → `ApplyPanelSizeConstraints` | Medium |

### Фаза 4: Инфраструктура (1-2 дня)

| # | Задача | Описание | Сложность |
|---|---|---|---|
| 4.1 | Централизовать версию через `Directory.Build.props` | Убрать дублирование Version | Low |
| 4.2 | Добавить error handling в fire-and-forget задачи | `Task.Run` → `try/catch` с логированием | Low |

### Фаза 5: Долгосрочные улучшения

| # | Задача | Описание | Сложность |
|---|---|---|---|
| 5.1 | DI-контейнер (Microsoft.Extensions.DependencyInjection) | Замена ручной компоновки сервисов | High |
| 5.2 | Имплементация IAsyncDisposable для NativeIntegrationService | Безопасное освобождение ресурсов | Medium |
| 5.3 | Миграция на CommunityToolkit.Mvvm | Уменьшение boilerplate в UI-классах | High |
| 5.4 | Разделение MainWindow.xaml на UserControl'ы | Декомпозиция XAML | High |

---

## Приоритеты

```
Критичные (P1-P4):  фазы 1-2
Средние (P5-P11):   фазы 3-4
Низкие (L1-L5):     фаза 4
```

**Общая оценка**: 12-18 дней при одном разработчике, или 6-9 дней при двух.
