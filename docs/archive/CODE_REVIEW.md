# Code Review: AiteBar v1.9.1

## Критические проблемы

### 1. Потенциальные проблемы потокобезопасности

**`MainWindow.xaml.cs:100-108`** — `DispatcherTimer` tick callback и SizeChanged handler работают с общими флагами `_shown`, `_isAnimating`, `_isPanelDragging` без синхронизации. При быстром переключении состояний возможны race conditions.

**`AppSettingsService.cs:362-386`** — `SaveAsync` вызывает `SettingsChanged` после записи на диск, но `Settings` setter тоже вызывает `SettingsChanged`. Это приводит к двойным событиям при сохранении.

### 2. Утечки ресурсов

**`MainWindow.xaml.cs:1071-1073`** — `CompleteDeferredStartupAsync().ContinueWith(...)` не отслеживается; если задача упадёт с необработанным исключением, `task.Exception` может быть `null` (нужно `task.Exception?`).

**`NativeIntegrationService.cs:62-65`** — Финализатор вызывает `UninstallMouseHook()`, но `Marshal.FreeHGlobal` не вызывается для выделенной памяти хука.

**`MainWindow.xaml.cs:1751-1753`** — `_startupCts` отменяется в `OnClosed`, но `Dispose()` вызывается в `CompleteDeferredStartupAsync`. Если `OnClosed` вызывается до завершения async-операции, возможна гонка.

### 3. Безопасность

**`ActionService.cs:263-274`** — `ExecuteCommand` выполняет произвольные команды через `cmd.exe /c`. Подтверждение пользователя — единственная защита. Regex-проверка в `ContainsPotentiallyDangerousCommandSyntax` не блокирует выполнение, только добавляет предупреждение.

**`MainWindow.xaml.cs:633`** — `Process.Start("explorer.exe", $"\"{target}\"")` — potential injection через path traversal. Если `target` содержит спецсимволы, это может привести к неожиданному поведению.

## Замечания по дизайну

### 4. Нарушение SRP

**`MainWindow.xaml.cs`** (1786 строк) — Класс нарушает Single Responsibility Principle: содержит UI-логику, обработку контекстного меню, drag-and-drop, позиционирование, анимации, горячие клавиши, локализацию и многое другое. Частичные классы частично решают проблему, но `MainWindow.xaml.cs` всё ещё слишком велик.

### 5. Дублирование кода

**`AppSettingsService.cs:61-198`** — `CloneAppSettings` содержит ~140 строк ручного копирования полей. При добавлении нового свойства в `AppSettings` легко забыть обновить клон.

**`UnifiedButtonService.cs:98-116`** — `GetUtilityVisibility` дублирует маппинг строковых ключей на свойства настроек, который также существует в `MainWindow` (switch-case для `SettingsKey`).

### 6. Магические числа

**`MainWindow.xaml.cs:16, 75-78`** — `DragHandleSpan = 18`, `PanelScreenPadding = 20`, `WheelDeltaPerContextSwitch = 120`, `ContextWheelSwitchCooldown = 220ms` — захардкожены без объяснения.

**`PanelLayoutHelper.cs:9-11`** — `ButtonOuterSize = 44`, `SeparatorSize = 9`, `PanelChrome = 8` — должны быть в `Constants.cs`.

## Проблемы производительности

### 7. Лишние аллокации

**`MainWindow.xaml.cs:260-261`** — `enabledContexts.ToList().FindIndex(...)` вызывается дважды подряд, создавая две промежуточные коллекции. Лучше вызвать `ToList()` один раз.

**`MainWindow.xaml.cs:1246`** — `_buttonImageCache.Clear()` при каждом `RefreshPanel()` сбрасывает кэш, даже если изображения не изменились.

### 8. Sync-over-async

**`TelemetryService.cs:23`** — `InitializeAsync().GetAwaiter().GetResult()` — блокирует поток. Если вызывается из UI-потока, возможен deadlock.

### 9. Logger блокирует I/O

**`Logger.cs:20-24`** — `File.AppendAllText` в lock-блоке блокирует потоки при каждом логировании. Лучше использовать `StreamWriter` с буферизацией или асинхронную запись.

## Мелкие замечания

### 10. Неиспользуемые поля/переменные

**`MainWindow.xaml.cs:1695`** — `_mouseWheelCaptureToken` объявлен, но нигде не используется.

**`MainWindow.xaml.cs:58`** — `_unifiedButtons` (тип `List<Button>`) содержит WPF-кнопки, но используется только для подсчёта количества. `Count` можно брать из `UnifiedButtonsPanel.Children`.

### 11. async void

**`MainWindow.xaml.cs:239, 339, 354, 680, 1053`** — Множество `async void` методов-обработчиков. Если внутри произойдёт необработанное исключение, приложение упадёт. Лучше обернуть тела в try/catch (что частично сделано в `Window_Loaded`, но не во всех).

### 12. Отсутствие IDisposable

**`MainWindow`** — Класс не реализует `IDisposable`, хотя владеет `_nativeService` (IDisposable), `_notifyIcon`, `_startupCts`. `OnClosed` пытается освободить ресурсы, но нет гарантии вызова.

## Рекомендации по приоритету

| Приоритет | Проблема | Рекомендация |
|-----------|----------|--------------|
| Высокий | Дублирование `CloneAppSettings` | Использовать source generator или deep copy через JSON |
| Высокий | `MainWindow` слишком большой | Вынести контекстные меню и утилитарную логику в отдельные классы |
| Средний | `async void` без try/catch | Обернуть все обработчики |
| Средний | Logger блокирует I/O | Перейти на асинхронное логирование |
| Средний | Магические числа | Перенести в `Constants.cs` |
| Низкий | `_mouseWheelCaptureToken` | Удалить неиспользуемое поле |
| Низкий | `ToListAsync()` дважды | Кэшировать результат |
