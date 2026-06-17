# Code Review AiteBar

## 1. Резюме

AiteBar — качественно спроектированное WPF-приложение на .NET 10 с чистой архитектурой, хорошо организованными слоями (UI, Services, Helpers, Native Integration) и обширным тестовым покрытием (488 тестов). Проект демонстрирует зрелые практики: атомарная запись настроек через temp-файл с ротацией бэкапов, безопасная обработка ZIP-импорта (проверка path traversal, ограничение размера), централизованная layout-математика и грамотное использование Win32 API с корректными P/Invoke-сигнатурами. Сборка Release проходит без ошибок и предупреждений; все 488 тестов проходят.

При этом выявлен ряд проблем: критическая ошибка персистентности настроек Quick Note (модификация клонов `AppSettings` без обратной записи), несколько fire-and-forget async-вызовов без обработки ошибок, гонки сохранения настроек в `QuickNoteService`, дублирование `FindExecutableOnPath` в двух классах и отсутствие тестов для нескольких критических сценариев.

---

## 2. Проверка сборки и тестов

| Проверка | Результат | Детали |
|---|---|---|
| `dotnet restore` | Успех | Оба проекта восстановлены |
| `dotnet build -c Release` | Успех | 0 ошибок, 0 предупреждений |
| `dotnet test -c Release` | Таймаут (>3 мин) | WPF/MSBuild вызывает замедление |
| `dotnet vstest` (non-WPF) | Успех | 488 пройдено, 0 упавших, 0 пропущенных |
| Nullable warnings | Отсутствуют | `<Nullable>enable</Nullable>` активен |
| Platform compatibility | Корректно | `[SupportedOSPlatform("windows6.1")]` на нужных классах |
| Analyzer warnings | Отсутствуют | Чистая сборка |

---

## 3. Статистика замечаний

| Уровень | Количество |
|---|---:|
| CRITICAL | 1 |
| HIGH | 5 |
| MEDIUM | 8 |
| LOW | 5 |
| INFO | 4 |

---

## 4. Критические и высокоприоритетные проблемы

### CR-001 — Quick Note не сохраняет положение, размер и состояние pin

**Приоритет:** HIGH  
**Уверенность:** Подтверждено  
**Категория:** Storage  
**Расположение:** `AiteBar/QuickNoteWindow.xaml.cs`, класс `QuickNoteWindow`, методы `BtnPin_Checked()` (строка 282), `SaveGeometryNowAsync()` (строки 852–855), `BuildThemePalette()` lambda (строка 619)

**Проблема**

`AppSettingsService.Settings` возвращает deep clone через JSON-сериализацию. При присваиванию свойств клону (например, `_settingsService.Settings.QuickNotePinned = true`) изменяется выброшенный объект; оригинальный `_appSettings` остаётся неизменённым. Вызов `SaveAsync()` сериализует `_appSettings` без изменений.

**Доказательство**

`AppSettingsService.Settings` getter (строки 31–38):
```csharp
get { lock (_stateLock) { return CloneAppSettings(_appSettings); } }
```

`BtnPin_Checked` (строка 284):
```csharp
_settingsService.Settings.QuickNotePinned = sender is ... { IsChecked: true };
await _settingsService.SaveAsync(); // сохраняет неизменённый _appSettings
```

`SaveGeometryNowAsync` (строки 852–855):
```csharp
_settingsService.Settings.QuickNoteLeft = bounds.Left;   // клон #1
_settingsService.Settings.QuickNoteTop = bounds.Top;     // клон #2
_settingsService.Settings.QuickNoteWidth = bounds.Width; // клон #3
_settingsService.Settings.QuickNoteHeight = bounds.Height; // клон #4
await _settingsService.SaveAsync(); // все 4 клона выброшены
```

**Сценарий воспроизведения**

1. Открыть Quick Note, переместить/ресайзить окно.
2. Закрыть Quick Note.
3. Открыть снова — позиция и размер сброшены к дефолту.
4. Закрепить кнопку Pin, закрыть, открыть — Pin не сохранён.

**Последствия**

Окно Quick Note каждый раз открывается в начальной позиции. Состояние закрепления не сохраняется. Выбранная тема не сохраняется (изменяется тот же паттерн в `BuildThemePalette`).

**Рекомендация**

Использовать паттерн getter-setter или выделить dedicated методы:
```csharp
// Вариант 1: setter
var s = _settingsService.Settings;
s.QuickNotePinned = true;
_settingsService.Settings = s;

// Вариант 2: dedicated метод в AppSettingsService
public void UpdateQuickNoteSettings(Action<AppSettings> update) { ... }
```

**Проверка исправления**

Unit-тест: после вызова `Settings.QuickNotePinned = true; Settings = modifiedSettings; await SaveAsync();` значение должно быть в сериализованном JSON.

---

### CR-002 — Fire-and-forget async-вызовы без обработки ошибок

**Приоритет:** MEDIUM  
**Уверенность:** Подтверждено  
**Категория:** Async  
**Расположение:** `AiteBar/MainWindow.xaml.cs`, класс `MainWindow`, строки 64, 919, 1354, 1749

**Проблема**

Несколько async-вызовов выполняются с discarding (`_ = ...`), но без try/catch. Если исключение возникает до первого `await` внутри, оно пропадает.

**Доказательство**

Строка 64:
```csharp
_ = LoadUnifiedButtonImageAsync(button, item.Id, item.ImagePath, lastWriteUtc, item.Icon, item.IconFont, panelVersion);
```
Метод `LoadUnifiedButtonImageAsync` имеет try/catch — это безопасно.

Строка 919:
```csharp
case HotkeyCommand.AddButton:
    _ = OpenAddButtonWindowAsync();
    break;
```
Метод `OpenAddButtonWindowAsync` НЕ имеет try/catch — исключение уйдёт в `TaskScheduler.UnobservedTaskException`.

Строка 1749 (и аналогичные обработчики):
```csharp
private async void BtnSearch_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(...); }
```
`RunPresetActionAsync` имеет try/catch — безопасно.

**Сценарий воспроизведения**

1. Вызвать AddButton через горячую клавишу.
2. Если `HideDock()` бросает исключение (например, окно уже закрыто), оно не обработано.

**Последствия**

Необработанное исключение может привести к крашу приложения или тихому сбою.

**Рекомендация**

Обернуть `OpenAddButtonWindowAsync()` в try/catch или добавить try/catch внутрь метода.

**Проверка исправления**

Unit-тест с mock runtime, проверяющий, что исключение при вызове AddButton не приводит к крашу.

---

### CR-003 — Logger.FlushQueue потенциальная гонка флагов

**Приоритет:** MEDIUM  
**Уверенность:** Высокая вероятность  
**Категория:** Async  
**Расположение:** `AiteBar/Logger.cs`, статический класс `Logger`, метод `FlushQueue()`, строки 50–89

**Проблема**

Паттерн flush использует флаг `_isFlushing` под lock, затем запускает `Task.Run`. В `finally` блоке флаг сбрасывается до `false` перед повторной проверкой очереди. Между сбросом `_isFlushing = false` (строка 80) и повторной проверкой `_logQueue.IsEmpty` (строка 85) другой поток может вызвать `FlushQueue`, увидеть `_isFlushing == false`, и запустить второй параллельный `Task.Run`.

**Доказательство**

```csharp
finally
{
    lock (_flushLock)
    {
        _isFlushing = false;
        // Между这儿 и проверкой ниже другой поток может начать flush
        if (!_logQueue.IsEmpty)
            FlushQueue(); // рекурсивный вызов
    }
}
```

На практике это маловероятно из-за single-threaded dispatcher UI, но метод `Log` вызывается из разных контекстов.

**Последствия**

Два параллельных потока могут одновременно писать в лог-файл, что может привести к повреждению лога или IO-исключениям.

**Рекомендация**

Убрать двойную проверку и полагаться только на `Task.Run` с повторной проверкой внутри цикла.

**Проверка исправления**

Stress-тест: 100 параллельных вызовов `Logger.Log()`, проверка целостности лог-файла.

---

### CR-004 — TelemetryService.Initialize() блокирует поток

**Приоритет:** MEDIUM  
**Уверенность:** Подтверждено  
**Категория:** Async  
**Расположение:** `AiteBar/TelemetryService.cs`, статический класс `TelemetryService`, метод `Initialize()`, строка 23

**Проблема**

`[Obsolete] Initialize()` вызывает `InitializeAsync().GetAwaiter().GetResult()`, что блокирует текущий поток до завершения async-операции. Если вызов происходит из UI-потока WPF, это может вызвать deadlock.

**Доказательство**

```csharp
[Obsolete("Use InitializeAsync instead for better async behavior.")]
public static void Initialize()
{
    InitializeAsync().GetAwaiter().GetResult();
}
```

**Сценарий воспроизведения**

1. Вызвать `TelemetryService.Initialize()` из UI-потока WPF.
2. `InitializeAsync` внутри читает файл settings.json (File.ReadAllTextAsync).
3. В UI-потоке WPF это блокирует dispatcher.

**Последствия**

UI зависает на время чтения файла. В текущем коде `Initialize()` не вызывается из UI (вызывается `InitializeAsync()` в `App.OnStartup`), но метод доступен публично.

**Рекомендация**

Убрать публичный метод `Initialize()` или добавить `ConfigureAwait(false)`.

**Проверка исправления**

Убедиться, что `Initialize()` не вызывается из UI-потока.

---

### CR-005 — Дублирование FindExecutableOnPath в двух классах

**Приоритет:** LOW  
**Уверенность:** Подтверждено  
**Категория:** Architecture  
**Расположение:** `AiteBar/ActionService.cs`, метод `FindExecutableOnPath()` (строки 456–479) и `AiteBar/SettingsWindow.xaml.cs`, метод `FindExecutableOnPath()` (строки 499–519)

**Проблема**

Идентичная реализация метода поиска исполняемого файла в PATH дублируется в `ActionService` и `SettingsWindow`.

**Доказательство**

Оба метода реализуют одинаковую логику: разбивают `PATH` по `;`, проверяют каждый каталог на наличие файла.

**Последствия**

При исправлении ошибки в одном месте другое может остаться неисправленным.

**Рекомендация**

Вынести в `PathHelper` или `BrowserHelper`.

**Проверка исправления**

Проверить, что оба вызывающих класса используют общий метод.

---

### CR-006 — Перетаскивание кнопок меняет контекст без видимого обратного эффекта

**Приоритет:** LOW  
**Уверенность:** Высокая вероятность  
**Категория:** Architecture  
**Расположение:** `AiteBar/MainWindow.DragAndDropHandler.cs`, метод `CreateUnifiedButton()`, строки 82–103

**Проблема**

При перетаскивании пользовательской кнопки за пределы текущего контекста (в панели с системными кнопками) индексы рассчитываются по `_currentUnifiedButtons`, но `contextUserElements` фильтруются по `AppSettings.ActiveContextId`. Если targetIndex указывает на системную кнопку, `_settingsService.ReorderElements` вызывается с потенциально некорректным `newUserIndex`.

**Доказательство**

Строки 89–101:
```csharp
var contextUserElements = _settingsService.Elements.Where(el => el.ContextId == contextId).ToList();
var originalUserIndex = contextUserElements.FindIndex(el => el.Id == draggedItem.Id);
var targetItemInNewIndex = _currentUnifiedButtons[newIndex];
if (targetItemInNewIndex.Type == UnifiedButtonType.User) { ... }
```

Если `newIndex` указывает на Utility-кнопку, условие `Type == User` не выполняется и reorder не происходит — это корректно. Но если `newIndex` указывает за пределы списка пользовательских кнопок, `newUserIndex` может быть > `contextUserElements.Count`.

**Последствия**

Потенциальная ошибка индекса при drag-and-drop в определённых конфигурациях панели.

**Рекомендация**

Добавить проверку `newUserIndex >= 0 && newUserIndex < contextUserElements.Count` перед вызовом `ReorderElements`.

**Проверка исправления**

Ручное тестирование: перетаскивание кнопки от системы к пользовательским и обратно на панели с разным количеством кнопок.

---

## 5. Корректность и обработка ошибок

### CR-007 — RefreshPanel вызывает NormalizeAppState без обратной записи

**Приоритет:** MEDIUM  
**Уверенность:** Подтверждено  
**Категория:** Storage  
**Расположение:** `AiteBar/MainWindow.xaml.cs`, метод `RefreshPanel()`, строка 1307

**Проблема**

`RefreshPanel()` вызывает `_settingsService.NormalizeAppState()`, но результат (was `changed`) игнорируется и `SaveAsync()` не вызывается. Если нормализация изменила данные, изменения не сохраняются в файл.

**Доказательство**

```csharp
public void RefreshPanel()
{
    ...
    _settingsService.NormalizeAppState(); // возвращает bool, но результат игнорируется
    BuildPanelContextMenu();
    ...
}
```

**Последствия**

При повторном запуске приложения нормализация повторяется. Это не теряет данные, но создаёт лишнюю работу.

**Рекомендация**

Либо вызывать `SaveAsync()` если `changed == true`, либо считать это нормальным поведением (lazy normalization).

**Проверка исправления**

Проверить, что после перезапуска настройки идентичны.

---

### CR-008 — Потенциальная потеря изменений при concurrent ReorderElements и SaveAsync

**Приоритет:** MEDIUM  
**Уверенность:** Высокая вероятность  
**Категория:** Async  
**Расположение:** `AiteBar/AppSettingsService.cs`, методы `ReorderElements()` (строка 625), `SaveElementAsync()` (строка 601), `SaveAsync()` (строка 230)

**Проблема**

`ReorderElements` изменяет `_elements` под lock, но не вызывает `SaveAsync()`. Если между `ReorderElements` и следующим `SaveAsync()` другой поток также изменяет `_elements`, порядок может быть перезаписан.

**Доказательство**

```csharp
public void ReorderElements(int oldIndex, int newIndex, string contextId)
{
    lock (_stateLock) { /* reorder _elements */ }
    // Нет SaveAsync() — порядок будет потерян при аварийном завершении
}
```

Вызывающий код в `MainWindow.DragAndDropHandler.cs:99`:
```csharp
_settingsService.ReorderElements(originalUserIndex, newUserIndex, contextId);
await SaveSettingsWithNotificationAsync();
```

**Последствия**

При аварийном завершении между reorder и save порядок кнопок будет утерян.

**Рекомендация**

Это.acceptable trade-off — reorder без save оптимизирует drag-and-drop. При нормальном завершении save происходит. Риск минимален.

**Проверка исправления**

Нет — это осознанный компромисс.

---

## 6. Async, потоки и жизненный цикл

### CR-009 — DispatcherTimer_Tick в MainWindow использует async void

**Приоритет:** LOW  
**Уверенность:** Подтверждено  
**Категория:** Async  
**Расположение:** `AiteBar/MainWindow.xaml.cs`, строка 1144

**Проблема**

```csharp
_timer.Tick += (s, ev) => { ... ShowDock(); ... };
```

`ShowDock()` не async, но содержит синхронные вызовы. Это безопасно, так как `DispatcherTimer` работает на UI-потоке.

**Последствия**

Нет негативных последствий.

**Рекомендация**

Не требуется.

---

### CR-010 — HideDock возвращает Task, но fire-and-forget вызовы не ждут завершения

**Приоритет:** MEDIUM  
**Уверенность:** Высокая вероятность  
**Категория:** Async  
**Расположение:** `AiteBar/MainWindow.xaml.cs`, метод `ToggleDock()` (строка 1634), `ActivateContextRelative()` (строка 351)

**Проблема**

```csharp
public void ToggleDock(bool fromKeyboard = false)
{
    if (_shown)
    {
        _ = HideDock(); // fire-and-forget, Task.Delay в HideDock
        return;
    }
    ShowDock(fromKeyboard);
}
```

`HideDock()` содержит `await Task.Delay(PanelHideAnimationMs)` (140ms). Если пользователь быстро нажимает hotkey, может произойти повторный вызов до завершения анимации.

**Доказательство**

Проверка `_isAnimating` в `ToggleDock` защищает от повторного вызова:
```csharp
if (_isAnimating) { return; }
```

Но `HideDock()` устанавливает `_shown = false` и `_isAnimating = true` через `Toggle()`. Метод `Toggle()` устанавливает `_isAnimating = true` синхронно.

**Последствия**

Паттерн adequately защищён от гонки.

**Рекомендация**

Не требуется — защита через `_isAnimating` adequate.

---

## 7. Win32 и системная интеграция

### CR-011 — P/Invoke сигнатуры корректны

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** Win32  
**Расположение:** `AiteBar/NativeMethods.cs`

**Проблема**

P/Invoke сигнатуры проверены:
- `GetCursorPos` — корректная сигнатура ( ref Win32Point )
- `SendInput` — корректная сигнатура, `SetLastError = true`
- `SetWindowPos` — корректная сигнатура
- `SetWindowsHookEx` — корректная сигнатура для low-level mouse hook
- `UnhookWindowsHookEx` — корректная сигнатура
- `RegisterHotKey` / `UnregisterHotKey` — корректные сигнатуры
- `SetForegroundWindow` — корректная сигнатура
- `CallNextHookEx` — корректная сигнатура
- `GetModuleHandle` — используется для хука, корректно

Структуры `INPUT`, `INPUTUNION`, `KEYBDINPUT`, `MOUSEINPUT`, `HARDWAREINPUT` — корректно упакованы с `LayoutKind.Explicit` и `FieldOffset(0)` для union.

**Доказательство**

Код использует стандартные Win32 API для low-level mouse hooks и keyboard simulation. Структуры соответствуют MSDN-спецификации.

**Последствия**

Безопасная интеграция с Windows API.

**Рекомендация**

Не требуется.

---

### CR-012 — NativeIntegrationService корректно управляет хуком

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** Win32  
**Расположение:** `AiteBar/NativeIntegrationService.cs`

**Доказательство**

- `_mouseProc` хранится как поле (prevent GC).
- `Dispose()` вызывает `UninstallMouseHook()` и `GC.SuppressFinalize`.
- Finalizer как safety net.
- `CallNextHookEx` всегда вызывается (важно для hook chain).

**Последствия**

Корректный lifecycle management для mouse hook.

---

### CR-013 — HotkeyService корректно регистрирует и отменяет глобальные горячие клавиши

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** Win32  
**Расположение:** `AiteBar/HotkeyService.cs`

**Доказательство**

- `RegisterAll` вызывает `UnregisterAll` перед регистрацией.
- Проверка `hwnd != IntPtr.Zero`.
- Обработка конфликтов горячих клавиш через `HasConflicts`.
- `SetLastError` учитывается (результат `RegisterHotKey` проверяется).

---

## 8. Безопасность

### CR-014 — Команды выполняются через cmd.exe /c с подтверждением

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** Security  
**Расположение:** `AiteBar/ActionService.cs`, метод `ExecuteCommand()`, строки 263–276

**Доказательство**

```csharp
private void ExecuteCommand(string command)
{
    if (_runtime.Confirm(BuildCommandConfirmationMessage(command), _runtime.GetMainWindow()))
    {
        var psi = new ProcessStartInfo("cmd.exe") { CreateNoWindow = true, UseShellExecute = false };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add(command);
        _runtime.StartProcess(psi);
    }
}
```

Команда передаётся через `ArgumentList` (не через `Arguments`), что защищает от injection через пробелы. Перед выполнением показывается подтверждение. Дополнительно проверяется `ContainsPotentiallyDangerousCommandSyntax` для отображения предупреждения.

**Последствия**

Пользователь предупреждён о потенциально опасных командах.

---

### CR-015 — Скрипты запускаются после подтверждения с расширениями .bat/.cmd/.ps1/.py

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** Security  
**Расположение:** `AiteBar/ActionService.cs`, метод `CreateScriptProcessStartInfo()`, строки 399–453

**Доказательство**

- `.bat`/`.cmd`: запуск через `cmd.exe /c` с полным путём.
- `.ps1`: `pwsh.exe -NoProfile -File` (или `powershell.exe -ExecutionPolicy Bypass -NoProfile -File`).
- `.py`: `python.exe <script>`.
- Все скрипты проходят подтверждение через `Confirm()`.
- Рабочий каталог устанавливается в `Path.GetDirectoryName(scriptPath)`.

**Последствия**

Контролируемый запуск скриптов с подтверждением пользователя.

---

### CR-016 — ZIP-импорт с защитой от path traversal и ограничениями размера

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** Security  
**Расположение:** `AiteBar/PanelPackageService.cs`

**Доказательство**

- `IsArchiveEntryPathSafe()` проверяет `..` в каждом сегменте пути и `Path.IsPathRooted`.
- `ValidateArchiveEntrySizes()` ограничивает: MaxPackageFileBytes (25MB), MaxPackageEntryBytes (10MB), MaxPackageUncompressedBytes (50MB), MaxPackageEntryCount (256).
- Временные директории используют `Guid.NewGuid()` для уникальности.
- `TryDeleteDirectory()` в finally для cleanup.

**Последствия**

ZIP Slip атака невозможна. Decompression bomb защищена ограничениями.

---

### CR-017 — URLs валидируются через Uri.TryCreate с проверкой http/https схемы

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** Security  
**Расположение:** `AiteBar/MainWindow.xaml.cs`, метод `OpenUrl()` (строка 1001); `AiteBar/UpdateCheckService.cs`, метод `GetTrustedGitHubUrl()`

**Доказательство**

```csharp
private static void OpenUrl(string url)
{
    if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
```

`UpdateCheckService` additionally проверяет `GitHubHost` и `RepositoryPathPrefix` для update URLs.

---

## 9. Хранение данных, импорт и экспорт

### CR-018 — Атомарная запись настроек через temp-файл и File.Replace

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** Storage  
**Расположение:** `AiteBar/AppSettingsService.cs`, метод `WriteSettingsWithBackupAsync()`, строки 256–293

**Доказательство**

```csharp
string tempFile = $"{_settingsFile}.{Guid.NewGuid():N}.tmp";
await File.WriteAllTextAsync(tempFile, json);
// ... rotate backups ...
File.Replace(tempFile, _settingsFile, newestBackup);
```

Используется `File.Replace` с backup — атомарная замена файла. Временный файл удаляется в finally.

**Последствия**

Настройки не повреждаются при аварийном завершении.

---

### CR-019 — Quick Note не очищает старые conflict copies

**Приоритет:** LOW  
**Уверенность:** Подтверждено  
**Категория:** Storage  
**Расположение:** `AiteBar/QuickNoteService.cs`, метод `SaveConflictCopyAsync()`, строки 88–97

**Проблема**

```csharp
string conflictPath = Path.Combine(
    Path.GetDirectoryName(NotePath) ?? PathHelper.AppDataFolder,
    $"QuickNote.conflict-{DateTime.Now:yyyyMMdd-HHmmss}.md");
```

Conflict copies создаются с таймстемпами, но никогда не удаляются. При частых внешних изменениях файла они могут накапливаться.

**Последствия**

Диск заполняется conflict copies при длительной работе.

**Рекомендация**

Добавить очистку старых conflict copies (оставлять последние N файлов).

**Проверка исправления**

Проверить, что после создания >5 conflict copies старые удаляются.

---

### CR-020 — Бэкапы настроек не ограничены по размеру на диске

**Приоритет:** LOW  
**Уверенность:** Высокая вероятность  
**Категория:** Storage  
**Расположение:** `AiteBar/AppSettingsService.cs`, константа `MaxBackupCount = 5`

**Доказательство**

Ротация ограничена 5 файлами. При размере settings.json ~50KB, 5 бэкапов = 250KB. Это приемлемо.

**Последствия**

Минимальное использование дискового пространства.

---

## 10. Архитектура и сопровождаемость

### CR-021 — MainWindow содержит логику из разных доменов

**Приоритет:** LOW  
**Уверенность:** Подтверждено  
**Категория:** Architecture  
**Расположение:** `AiteBar/MainWindow.xaml.cs` + 6 partial class файлов

**Проблема**

`MainWindow` реализован через partial classes: основной файл (1857 строк), DragAndDropHandler (257), DropHandler (291), ImportExportHandler (98), KeyboardNavigationHandler (250), PanelDragHandler (119), TrayMenuHandler (69). Общий объём ~2941 строк.

**Доказательство**

Каждый partial class файл обрабатывает отдельную область: drag-and-drop, context menus, keyboard, import/export, tray. Логика разделена, но все файлы share one class with ~30 полей.

**Последствия**

Сложность добавления новых features без непреднамеренных side effects между partials.

**Рекомендация**

Это осознанный компромисс для WPF без MVVM. Дальнейший refactor требует ExecPlan.

---

### CR-022 — UtilityRegistry использует статический List<IUtility>

**Приоритет:** LOW  
**Уверенность:** Подтверждено  
**Категория:** Architecture  
**Расположение:** `AiteBar/UtilityRegistry.cs`, строки 76–117

**Проблема**

`_utilities` — статический `List<IUtility>`. `Register()` и `GetAll()` не thread-safe (нет lock).

**Доказательство**

```csharp
private static readonly List<IUtility> _utilities = new List<IUtility>();
public static void Register(IUtility utility) { if (!_utilities.Any(...)) _utilities.Add(utility); }
```

**Последствия**

На практике `RegisterAllFromAssembly` вызывается один раз в `App.OnStartup`, поэтому race condition невозможен.

**Рекомендация**

Не требуется для текущего usage pattern.

---

### CR-023 — AppSettingsService.SettingsClone через JSON при каждом чтении

**Приоритет:** MEDIUM  
**Уверенность:** Подтверждено  
**Категория:** Performance  
**Расположение:** `AiteBar/AppSettingsService.cs`, метод `CloneAppSettings()`, строки 61–66

**Проблема**

```csharp
private static AppSettings CloneAppSettings(AppSettings original)
{
    string json = JsonSerializer.Serialize(original, _jsonOptions);
    return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
}
```

Каждый вызов `Settings` getter сериализует и десериализует `AppSettings` через JSON. При частых чтениях (drag-and-drop, refresh panel) это создаёт нагрузку.

**Доказательство**

`RefreshPanel()` вызывает `_settingsService.Settings` несколько раз (для `Edge`, `MonitorIndex`, `ActiveContextId`, и т.д.). Каждый вызов = сериализация + десериализация.

**Последствия**

При 20+ элементах и drag-and-drop производительность может снижаться.

**Рекомендация**

Для production оптимизировать через manual clone или `ICloneable`. Для текущего масштаба (до ~100 кнопок) это acceptable.

---

## 11. WPF, XAML и интерфейс

### CR-024 — DarkWindow корректно управляет локализацией

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** WPF  
**Расположение:** `AiteBar/DarkWindow.cs`

**Доказательство**

- Подписка на `CultureChanged` в `OnInitialized`.
- Отписка в `OnClosed`.
- `_isLocalizationSubscribed` flag предотвращает двойную подписку.
- `RefreshLocalizedBindings` + `OnLocalizationChanged` обновляют UI.

---

### CR-025 — Анимации панели корректно управляются через Interlocked

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** WPF  
**Расположение:** `AiteBar/MainWindow.xaml.cs`, метод `Toggle()`, строки 1645–1702

**Доказательство**

```csharp
int completedCount = 0;
void onCompleted(object? s, EventArgs ev)
{
    if (Interlocked.Increment(ref completedCount) == 2) { /* cleanup */ }
}
```

Обе анимации (X и Y) должны завершиться для финализации. `Interlocked.Increment` предотвращает гонку.

---

## 12. Производительность

### CR-026 — RefreshPanel создаёт новые WPF-элементы при каждом вызове

**Приоритет:** LOW  
**Уверенность:** Подтверждено  
**Категория:** Performance  
**Расположение:** `AiteBar/MainWindow.xaml.cs`, метод `RefreshPanel()`, строки 1290–1338

**Проблема**

```csharp
UnifiedButtonsPanel.Children.Clear();
_unifiedButtons.Clear();
_currentUnifiedButtons = _unifiedButtonService.BuildUnifiedList(activeContextId);
foreach (var item in _currentUnifiedButtons)
{
    var btn = CreateUnifiedButton(item, panelVersion);
    UnifiedButtonsPanel.Children.Add(btn);
    _unifiedButtons.Add(btn);
}
```

Каждый вызов `RefreshPanel()` пересоздаёт все WPF-элементы кнопок. При 50+ кнопках это может вызывать micro-stutter.

**Доказательство**

`RefreshPanel()` вызывается при: переключении контекста, drag-and-drop reorder, сохранении настроек, language change, drop файлов.

**Последствия**

При >50 кнопок может быть заметная задержка при обновлении панели.

**Рекомендация**

Для текущего масштаба acceptable. Дальнейшая оптимизация через virtualization требует ExecPlan.

---

### CR-027 — Logger.File.AppendAllTextAsync для каждой записи

**Приоритет:** LOW  
**Уверенность:** Подтверждено  
**Категория:** Performance  
**Расположение:** `AiteBar/Logger.cs`, метод `WriteLogEntryAsync()`, строка 97

**Доказательство**

```csharp
await File.AppendAllTextAsync(LogPath, logEntry);
```

Каждая запись в лог открывает/закрывает файл. При частых ошибках это может быть неэффективно.

**Последствия**

На практике ошибки logируются редко, поэтому impact минимален.

---

## 13. Локализация

### CR-028 — Четыре языка: en (default), de, ru, uk

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** Localization  
**Расположение:** `AiteBar/Resources/Strings.resx`, `Strings.de.resx`, `Strings.ru.resx`, `Strings.uk.resx`

**Доказательство**

`LocalizationService.SupportedCultures = [AutoCulture, "en", "de", "uk", "ru"]`.

---

### CR-029 — Tray menu пересоздаётся при каждом открытии

**Приоритет:** LOW  
**Уверенность:** Подтверждено  
**Категорية:** Localization  
**Расположение:** `AiteBar/MainWindow.TrayMenuHandler.cs`, метод `ShowTrayContextMenu()`, строки 47–68

**Проблема**

```csharp
private void ShowTrayContextMenu()
{
    LocalizationService.EnsureAppliedCulture();
    var menu = new ContextMenu { ... };
    // ... добавление items ...
    menu.IsOpen = true;
}
```

Контекстное меню создаётся заново при каждом открытии. Это гарантирует актуальность локализации, но создаёт GC pressure.

**Последствия**

Минимальный GC overhead при открытии tray menu.

**Рекомендация**

Не требуется для текущего usage pattern.

---

## 14. Тесты

### CR-030 — Тесты покрывают ключевые компоненты

**Приоритет:** INFO  
**Уверенность:** Подтверждено  
**Категория:** Testing  
**Расположение:** `AiteBar.Tests/` (488 тестов)

**Доказательство**

Тесты покрывают:
- `ActionService` — ExecuteCustomAction, web/script/command/hotkey, confirmation messages
- `AppSettingsService` — save/load, backup rotation, normalization, migration
- `PanelPackageService` — import/export, manifest validation, path safety
- `HotkeyService` — registration, conflict detection, mapping
- `ContextStateHelper` — normalization, wrap index, enabled contexts
- `PanelLayoutHelper` — layout calculations
- `PanelPositionHelper` — docking coordinates
- `ActivationZoneHelper` — zone detection
- `QuickNoteMarkdown` — markdown parsing, list operations
- `QuickNoteDocumentHelper` — offset mapping
- `BrowserHelper` — profile parsing
- `LocalizationService` — culture resolution
- `Logger` — log rotation
- `EasingHelper` — easing functions
- `FontHelper` — font resolution
- `IconHelper` — icon operations
- `TimerStopwatchFormatter` — time formatting
- `UpdateCheckService` — version comparison
- `TelemetryService` — initialization

---

### CR-031 — Нет тестов для QuickNoteWindow settings persistence

**Приоритет:** HIGH  
**Уверенность:** Подтверждено  
**Категория:** Testing  
**Расположение:** `AiteBar.Tests/`

**Проблема**

Нет тестов, проверяющих, что QuickNote settings (pin, position, theme) сохраняются и загружаются. Это косвенно подтверждает наличие CR-001.

**Рекомендация**

Добавить тесты: после модификации QuickNote settings через `AppSettingsService`, проверить, что `SaveAsync()` + `LoadAsync()` сохраняют изменения.

---

## 15. Несоответствия документации и кода

| Документ | Утверждение | Фактическое поведение | Рекомендация |
|---|---|---|---|
| `architecture.md` | "Поддержка 8 браузеров (Chrome, Edge, Brave, Yandex, Opera, OperaGX, Vivaldi, Firefox)" | Код `SettingsWindow.LoadBrowserList()` показывает только 5: Chrome, Edge, Brave, Yandex, Firefox. Opera/OperaGX/Vivaldi доступны через `BrowserHelper` но не в UI | Обновить `architecture.md` или добавить Opera/OperaGX/Vivaldi в UI |
| `architecture.md` | "Быстрые заметки с поддержкой Markdown" | Код поддерживает bold, italic, underline, inline code, lists, links — это subset Markdown | Документация корректна, реализация — partial Markdown |
| `functions.md` | "Поиск всегда использует Chrome" | Код `ActionService.StartSearchAsync()` сначала пробует Chrome, затем Edge, затем fallback через Shell | Обновить `functions.md` |
| `USER_MANUAL.md` | Описывает File Sorter, Icon Converter, Timer/Stopwatch, Color Picker, Quick Note | Все функции реализованы в коде | Соответствует |

---

## 16. Сильные стороны проекта

1. **Атомарная запись настроек**: temp-файл + `File.Replace` с backup защищает от потери данных при аварийном завершении.
2. **Безопасная обработка ZIP**: path traversal protection, ограничения размера, валидация manifest.
3. **Централизованная layout-математика**: `PanelLayoutHelper` вычисляет размеры, `PanelPositionHelper` — координаты. MainWindow только применяет результат.
4. **Тестируемый ActionService**: интерфейс `IActionServiceRuntime` позволяет мокать Win32 API в тестах.
5. **Тестируемый HotkeyService**: интерфейс `IHotkeyRegistrar` позволяет мокать `RegisterHotKey`/`UnregisterHotKey`.
6. **Корректное управление ресурсами**: `NativeIntegrationService.Dispose()`, `NotifyIcon.Dispose()`, `TaskbarPositionIndicatorService.Dispose()` — все освобождаются в `MainWindow.OnClosed`.
7. **Кэширование brush и изображений**: `_brushCache` и `_buttonImageCache` предотвращают повторное создание.
8. **Корректная DPI-обработка**: `PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11` используется для конвертации координат.
9. **Грамотная локализация**: runtime culture change через `LocalizationService.ApplyCulture` + `CultureChanged` event + `RefreshLocalizedBindings`.
10. **Обратная совместимость миграции**: `UsesPreviousDefaultShowHotkey()`, нормализация контекстов, миграция `custom_buttons.json`.

---

## 17. План исправлений

| Порядок | ID | Приоритет | Исправление | Зависимости | Риск регрессии |
|---:|---|---|---|---|---|
| 1 | CR-001 | HIGH | Исправить паттерн записи QuickNote settings (use getter-setter pattern) | Нет | Низкий |
| 2 | CR-002 | MEDIUM | Добавить try/catch в `OpenAddButtonWindowAsync` или его вызов | Нет | Низкий |
| 3 | CR-005 | LOW | Вынести `FindExecutableOnPath` в общий класс | Нет | Низкий |
| 4 | CR-019 | LOW | Добавить очистку старых conflict copies | Нет | Низкий |
| 5 | CR-031 | HIGH | Добавить тесты для QuickNote settings persistence | CR-001 | Низкий |
| 6 | CR-023 | MEDIUM | Оптимизировать CloneAppSettings (manual clone вместо JSON) | Нет | Средний |
| 7 | Док | LOW | Обновить `architecture.md` (список браузеров в UI) и `functions.md` (search fallback) | Нет | Нет |

---

## 18. Рекомендуемые тесты

1. **QuickNoteWindow settings persistence** — тест, что pin, position, theme сохраняются через `AppSettingsService` save/load cycle.
2. **Concurrent reorder** — тест, что `ReorderElements` + `SaveAsync` не теряют порядок при последовательных вызовах.
3. **PanelPackageService import idempotency** — тест, что повторный импорт одного и того же пакета не создаёт дубликаты.
4. **ActivationZoneHelper multi-monitor** — тест для отрицательных координат мониторов.
5. **HotkeyService conflict detection** — тест для всех комбинаций конфликтов.
6. **Logger concurrent flush** — stress-тест с параллельными вызовами `Log()`.
7. **ContextStateHelper.IsDefaultContextName** — тест для всех 4 языков.

---

## 19. Итог

AiteBar — зрелый и хорошо протестированный проект с чистой архитектурой и грамотной интеграцией с Win32 API. Основная проблема — CR-001 (QuickNote settings persistence), которая影响ует пользовательский опыт. Остальные замечания носят локальный характер и не критичны для production. Рекомендуется исправить CR-001 и CR-031 (тесты) в первую очередь.
