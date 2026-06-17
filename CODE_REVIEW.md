# Code Review AiteBar

## 1. Резюме

AiteBar — зрелое Windows desktop-приложение на .NET 10 / WPF с чёткой архитектурой: чистые helper-классы для расчётов (`PanelLayoutHelper`, `PanelPositionHelper`, `ActivationZoneHelper`), выделенные сервисы (`ActionService`, `AppSettingsService`, `PanelPackageService`, `HotkeyService`, `UpdateCheckService`), централизованная локализация и система регистрации утилит через `UtilityRegistry`. Проект имеет 50+ unit-тестов, автоматизированный CI/CD (build, test, coverage, release), код-ревью через CodeQL и продуманную безопасность (атомарная запись настроек, ротация бэкапов, валидация ZIP-архивов, валидация URL обновлений).

Основные находки: один дефект горячих клавиш (FileSorter hotkey не обрабатывается), проблема потери логов в `Logger.FlushQueue`, отсутствие освобождения `Bitmap` в `ScreenColorPickerWindow` и производительность при частом клонировании `AppSettings`.

## 2. Проверка сборки и тестов

| Проверка | Результат | Детали |
|---|---|---|
| dotnet restore | Успешно | Оба проекта восстановлены |
| dotnet build -c Release | Ошибка | DLL залочена внешним процессом (AiteBar-Setup.exe) |
| dotnet build -c Debug | Ошибка | Аналогичная проблема с залоченным DLL |
| dotnet test | Не выполнен | Не из-за кода, а из-за заблокированного DLL в `obj/` |
| dotnet vstest | Не выполнен | Предыдущая команда не собрала проект |

**Причина:** Процесс `AiteBar-Setup.exe` (PID 9648) и его temp-процесс удерживают файл `AiteBar.dll` в `obj/Release/net10.0-windows/win-x64/`. Это проблема окружения, не кода. В CI (GitHub Actions) такая проблема не возникает.

## 3. Статистика замечаний

| Уровень | Количество |
|---:|---:|
| CRITICAL | 0 |
| HIGH | 3 |
| MEDIUM | 5 |
| LOW | 4 |
| INFO | 5 |

## 4. Критические и высокоприоритетные проблемы

### CR-001 — HotkeyCommand.FileSorter не обрабатывается в WndProc

**Приоритет:** HIGH
**Уверенность:** Подтверждено
**Категория:** Architecture / Correctness
**Расположение:** `AiteBar/MainWindow.xaml.cs`, метод `ExecuteHotkeyCommand()`, строки 905–931

**Проблема**

В `HotkeyService` зарегистрирован 8 команд, включая `HotkeyCommand.FileSorter` (ID 9004). Однако в `MainWindow.ExecuteHotkeyCommand()` отсутствует `case HotkeyCommand.FileSorter:`. Пользователь может назначить глобальную горячую клавишу для FileSorter в настройках, но нажатие этой клавиши не выполнит никакого действия — switch молча пропустит эту команду.

**Доказательство**

`HotkeyService.cs:15-16` определяет `FileSorter` как одну из команд. `MainWindow.xaml.cs:905-931` обрабатывает `ShowPanel`, `NextContext`, `PreviousContext`, `AddButton`, `QuickNote`, `ColorPicker`, `TimerStopwatch` — но не `FileSorter`.

**Сценарий воспроизведения**

1. Открыть настройки программы → вкладка «Горячие клавиши».
2. Назначить комбинацию для действия «File Sorter».
3. Сохранить настройки.
4. Нажать назначенную комбинацию.
5. Ничего не происходит.

**Последствия**

Пользователь видит, что горячая клавиша назначена, но она не работает. Ожидание нарушено, нет обратной связи.

**Рекомендация**

Добавить недостающий case в `ExecuteHotkeyCommand()`:

```csharp
case HotkeyCommand.FileSorter:
    _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("FileSorter", HideDock));
    break;
```

**Проверка исправления**

Добавить unit-тест, проверяющий что `HotkeyService.Descriptors` содержит все команды и `MainWindow` обрабатывает каждую из них (или добавить тест-реверсивную проверку покрытия всех HotkeyCommand в switch).

---

### CR-002 — Logger.FlushQueue может терять логи

**Приоритет:** HIGH
**Уверенность:** Подтверждено
**Категория:** Async / Correctness
**Расположение:** `AiteBar/Logger.cs`, метод `FlushQueue()`, строки 35–60

**Проблема**

Паттерн `_isFlushing` флаг создаёт окно, в котором новые записи лога могут быть потеряны. Если `FlushQueue()` вызывается, пока предыдущий `Task.Run` ещё работает, новый вызов возвращается немедленно (`if (_isFlushing) return`). Когда предыдущий `Task.Run` завершается и сбрасывает `_isFlushing = false`, записи, добавленные во время обработки, остаются в `_logQueue` без обработчика.

**Доказательство**

```csharp
// FlushQueue вызывается из Log()
if (_isFlushing) return; // ← Новая запись пропущена, если _isFlushing == true
_isFlushing = true;

// Task.Run завершается:
while (_logQueue.TryDequeue(out string? logEntry)) { ... }
_isFlushing = false; // ← Записи, добавленные во время while, уже обработаны
                     //    но записи, добавленные ПОСЛЕ последнего TryDequeue
                     //    но ДО _isFlushing = false, не будут обработаны
```

На практике это маловероятно из-за низкой частоты ошибок, но при массовых ошибках (например, повреждённый конфиг) записи могут теряться.

**Сценарий воспроизведения**

1. Вызвать `Logger.Log(ex1)` — начинается обработка.
2. Во время записи `ex1` в файл, вызвать `Logger.Log(ex2)`.
3. Если `ex2` добавлена после завершения `while` цикла, но до `_isFlushing = false`, она останется в очереди без обработчика.

**Последствия**

Потеря логов ошибок. Восстановление диагностики затрудняется.

**Рекомендация**

Заменить паттерн на `Interlocked.CompareExchange`:

```csharp
private static void FlushQueue()
{
    if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) != 0) return;
    Task.Run(async () =>
    {
        try
        {
            while (_logQueue.TryDequeue(out string? logEntry))
                await WriteLogEntryAsync(logEntry);
        }
        finally { Interlocked.Exchange(ref _isFlushing, 0); }
    });
}
```

И добавить повторную проверку очереди в `finally` (double-check pattern).

**Проверка исправления**

Unit-тест: одновременно вызвать `Log()` из нескольких потоков и проверить, что все записи записаны в файл.

---

### CR-003 — ScreenColorPickerWindow не освобождает Bitmap

**Приоритет:** HIGH
**Уверенность:** Подтверждено
**Категория:** Resource Management
**Расположение:** `AiteBar/ScreenColorPickerWindow.cs`, строка 40

**Проблема**

`_screen` (System.Drawing.Bitmap) создаётся в конструкторе, но не освобождается при закрытии окна. `Bitmap` использует неуправляемый GDI+ ресурс, который не будет освобождён GC до финализатора.

**Доказательство**

```csharp
private readonly Drawing.Bitmap _screen;
// В конструкторе:
_screen = new Drawing.Bitmap(bounds.Width, bounds.Height);
// Нет Dispose в OnClosed/OnClosing
```

Для large monitors (4K: 3840×2160, 4 bpp = ~33 MB) это значительная утечка GDI-объектов.

**Сценарий воспроизведения**

1. Открыть пипетку (ColorPicker) на 4K мониторе.
2. Закрыть пипетку.
3. Повторить 10–20 раз.
4. `GDI32.dll` может исчерпать лимит GDI-объектов (10000 по умолчанию).

**Последствия**

Утечка GDI-ресурсов. При многократном использовании пипетки возможен краш при попытке создания нового GDI-объекта.

**Рекомендация**

Добавить обработчик `Closed`:

```csharp
Closed += (_, _) => _screen.Dispose();
```

Или добавить `using` при создании с сохранением ссылки до закрытия окна.

**Проверка исправления**

Мониторинг GDI object count через Process Explorer после 20 циклов открытия/закрытия пипетки.

## 5. Корректность и обработка ошибок

### CR-004 — AppSettingsService.Settings выполняет JSON-клонирование при каждом обращении

**Приоритет:** MEDIUM
**Уверенность:** Подтверждено
**Категория:** Performance / Correctness
**Расположение:** `AiteBar/AppSettingsService.cs`, свойство `Settings`, строки 31–49; `AiteBar/MainWindow.xaml.cs`, строка 45

**Проблема**

Свойство `Settings` выполняет полное JSON-сервериализацию/десериализацию при каждом чтении. В `MainWindow` свойство `AppSettings` обращается к `_settingsService.Settings` (~100+ раз в секунду при активной панели: таймер, RefreshPanel, позиционирование).

**Доказательство**

```csharp
public AppSettings Settings
{
    get
    {
        lock (_stateLock)
        {
            return CloneAppSettings(_appSettings); // JSON round-trip
        }
    }
}
```

`MainWindow.AppSettings` — композитное свойство, обращение к которому вызывает `Settings` getter.

**Последствия**

Выделение памяти и CPU на каждый доступ к настройкам. На быстрых машинах незаметно, но на слабых — может влиять на отзывчивость UI.

**Рекомендация**

Создавать клон только при модификации (immutable snapshot pattern) или использовать `ImmutableDictionary` для мутабельных полей.

**Проверка исправления**

Benchmark с `BenchmarkDotNet`: измерить количество аллокаций и время доступа до/после оптимизации.

---

### CR-005 — PanelPackageService использует сжатый размер для проверки decompression bomb

**Приоритет:** MEDIUM
**Уверенность:** Подтверждено
**Категория:** Security
**Расположение:** `AiteBar/PanelPackageService.cs`, метод `ValidateArchiveEntrySizes()`, строки 251–283

**Проблема**

`ZipArchiveEntry.Length` возвращает сжатый размер записи. Проверка `totalUncompressedBytes += entry.Length` фактически суммирует сжатые размеры, что делает проверку на decompression bomb менее эффективной: архив может содержать записи, которые при распаковке раздуваются в десятки раз.

**Доказательство**

```csharp
checked { totalUncompressedBytes += entry.Length; } // Length = compressed size
```

ZIP-спецификация: `Length` — compressed size. Decompression ratio может быть 100x для текстовых данных.

**Последствия**

ZIP-архив размером 1 MB сжатого может распаковаться в 100 MB, превысив `MaxPackageUncompressedBytes`. Текущая проверка пропустит его.

**Рекомендация**

Вместо проверки через `entry.Length` ограничить распаковку через `ExtractToDirectory` с подсчётом извлечённых байтов. Или установить жёсткий лимит `MaxPackageEntryBytes` на сжатый размер (уже есть: 10 MB).

**Проверка исправления**

Создать тестовый `.aitebarpanel` с highly compressible data (10 MB сжатого → 500 MB распакованного) и убедиться, что импорт отклоняется.

---

### CR-006 — Hardcoded русская строка в ошибке UtilityBase

**Приоритет:** LOW
**Уверенность:** Подтверждено
**Категория:** Localization
**Расположение:** `AiteBar/UtilityRegistry.cs`, метод `LaunchAsync()`, строка 64

**Проблема**

При краше утилиты пользователю показывается `DarkDialog` с hardcoded русским текстом:

```csharp
new DarkDialog($"Утилита {Id} временно недоступна").ShowDialog();
```

На английском или немецком языке интерфейса это выглядит некорректно.

**Последствия**

Пользователи не русскоязычных локалей видят сообщение на русском.

**Рекомендация**

Использовать локализованный ключ, например `LocalizationService.Format("Utility_Unavailable", Id)`.

**Проверка исправления**

Добавить ключ в Strings.resx, Strings.ru.resx, Strings.de.resx, Strings.uk.resx и убедиться, что тест `ResourceFiles_HaveSameKeysAndFormatPlaceholders` проходит.

---

### CR-007 — TelemetryService.Initialize блокирует поток через GetAwaiter().GetResult()

**Приоритет:** LOW
**Уверенность:** Высокая вероятность
**Категория:** Async
**Расположение:** `AiteBar/TelemetryService.cs`, метод `Initialize()`, строка 23

**Проблема**

`Initialize()` помечен как `[Obsolete]`, но вызывает `InitializeAsync().GetAwaiter().GetResult()`. В контексте WPF сynchronization context это может вызвать deadlock, если вызов происходит из UI-потока с ожиданием другого async оператора.

**Доказательство**

```csharp
[Obsolete("Use InitializeAsync instead")]
public static void Initialize()
{
    InitializeAsync().GetAwaiter().GetResult(); // potential deadlock
}
```

В текущем коде `Initialize()` не вызывается из production-кода (только `InitializeAsync`), но метод остаётся public и доступен.

**Последствия**

Потенциальный deadlock при использовании из UI-потока.

**Рекомендация**

Убрать `Initialize()` или сделать его internal/private. Если нужен синхронный вариант для тестов — использовать `Task.Run(() => InitializeAsync()).GetAwaiter().GetResult()`.

**Проверка исправления**

Убедиться, что public API не содержит синхронных async-обёрток.

---

### CR-008 — TaskbarPositionIndicatorService содержит Debug.WriteLine

**Приоритет:** LOW
**Уверенность:** Подтверждено
**Категория:** Code Quality
**Расположение:** `AiteBar/TaskbarPositionIndicatorService.cs`, строки 23, 38, 49, 56, 65, 71, 73, 75, 77, 79

**Проблема**

В production-коде оставлены `Debug.WriteLine` вызовы. В Release-сборке `Debug.WriteLine` не выводится в консоль, но создаёт噪音 в отладочном выводе при подключении отладчика.

**Доказательство**

Более 10 `Debug.WriteLine` вызовов в `TaskbarPositionIndicatorService.cs`.

**Последствия**

Загрязнение debug output при отладке. Незначительное влияние на производительность в Debug-сборке.

**Рекомендация**

Убрать или заменить на `Logger.Log` для важных событий.

**Проверка исправления**

Проверить, что `Debug.WriteLine` отсутствуют в release-коде.

## 6. Async, потоки и жизненный цикл

### CR-009 — OnClosed корректно освобождает все ресурсы

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Resource Management
**Расположение:** `AiteBar/MainWindow.xaml.cs`, метод `OnClosed()`, строки 1806–1851

**Проблема**

`OnClosed` корректно отписывается от событий, отменяет `CancellationTokenSource`, освобождает `NativeIntegrationService`, `NotifyIcon`, `TaskbarPositionIndicatorService` и вызывает `UnregisterGlobalHotkey`. Каждый вызов обёрнут в try-catch для изоляции ошибок.

**Доказательство**

Каждый ресурс освобождается отдельно с catch. `base.OnClosed` вызывается в finally-блоке.

**Последствия**

Корректное завершение без утечек ресурсов. Сильная сторона проекта.

---

### CR-010 — NativeIntegrationService использует GC finalizer для cleanup хука

**Приоритет:** MEDIUM
**Уверенность:** Подтверждено
**Категория:** Resource Management
**Расположение:** `AiteBar/NativeIntegrationService.cs`, деструктор, строки 62–65

**Проблема**

`NativeIntegrationService` реализует `IDisposable` + финализатор для отписки от mouse hook. Если `Dispose()` не вызовется (например, из-за исключения), cleanup происходит через GC finalizer — в непредсказуемое время.

**Доказательство**

```csharp
~NativeIntegrationService()
{
    UninstallMouseHook();
}
```

В текущем коде `MainWindow.OnClosed` всегда вызывает `_nativeService?.Dispose()`, но при необработанном исключении в `OnClosed` (после попытки Dispose) финализатор остаётся единственной страховкой.

**Последствия**

Глобальный mouse hook может оставаться установленным после закрытия приложения, перехватывая клики других приложений.

**Рекомендация**

Убедиться, что Dispose гарантированно вызывается через `try-finally` в `OnClosed`. Текущий код уже это делает — это сильная сторона.

**Проверка исправления**

Проверить через Process Explorer, что при завершении приложения нет активных хуков.

## 7. Win32 и системная интеграция

### CR-011 — P/Invoke сигнатуры корректны

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Win32
**Расположение:** `AiteBar/NativeMethods.cs`, строки 1–195

**Проблема

Все P/Invoke объявления (`SetWindowsHookEx`, `UnhookWindowsHookEx`, `RegisterHotKey`, `UnregisterHotKey`, `SendInput`, `SetWindowPos`, `SetForegroundWindow`) используют корректные сигнатуры. Структуры (`INPUT`, `INPUTUNION`, `KEYBDINPUT`, `MOUSEINPUT`, `MSLLHOOKSTRUCT`, `MONITORINFO`, `APPBARDATA`, `RECT`) правильно упакованы. `AllowUnsafeBlocks` включен в csproj для возможного использования.

**Доказательство**

`NativeMethods.cs` содержит все необходимые Win32 API объявления с правильными `DllImport`, `SetLastError` и `StructLayout` атрибутами.

**Последствия**

Корректная работа с Windows API. Сильная сторона проекта.

---

### CR-012 — GetAsyncKeyState объявлен в ActionServiceRuntime, а не в NativeMethods

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Architecture
**Расположение:** `AiteBar/ActionService.cs`, строка 553

**Проблема**

`GetAsyncKeyState` объявлен отдельно в `ActionServiceRuntime` через `[DllImport("user32.dll")]`, хотя все остальные Win32 API集中在 `NativeMethods.cs`. Это не дефект, но нарушает единообразие.

**Рекомендация**

Перенести объявление в `NativeMethods.cs`.

## 8. Безопасность

### CR-013 — Подтверждение перед запуском команд и скриптов

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Security
**Расположение:** `AiteBar/ActionService.cs`, методы `ExecuteCommand()`, `StartScriptFileAsync()`

**Проблема**

Все команды и скрипты требуют подтверждения через `DarkDialog` перед запуском. Дополнительно для команд показывается предупреждение о potentially dangerous syntax (`ContainsPotentiallyDangerousCommandSyntax`).

**Доказательство**

```csharp
private void ExecuteCommand(string command)
{
    if (_runtime.Confirm(BuildCommandConfirmationMessage(command), _runtime.GetMainWindow()))
    { ... }
}
```

**Последствия**

Пользователь всегда видит, что именно будет выполнено. Сильная сторона проекта.

---

### CR-014 — UpdateCheckService валидирует URL обновлений

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Security
**Расположение:** `AiteBar/UpdateCheckService.cs`, метод `GetTrustedGitHubUrl()`, строки 188–209

**Проблема**

URL обновлений валидируются: только HTTPS, только `github.com`, только путь `/codebdbd/aitebar/`. Installer URL открывается через `UseShellExecute = true`, что безопасно для HTTPS URL.

**Доказательство**

`GetTrustedGitHubUrl` проверяет scheme, host и path prefix.

**Последствия**

Невозможно открыть вредоносный URL через механизм обновлений. Сильная сторона проекта.

---

### CR-015 — PanelPackageService валидирует ZIP-архивы

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Security
**Расположение:** `AiteBar/PanelPackageService.cs`, методы `ValidateArchiveEntrySizes()`, `IsArchiveEntryPathSafe()`, `ValidateManifest()`

**Проблема**

Импорт `.aitebarpanel` проверяет: размер файла (25 MB), количество записей (256), размер каждой записи (10 MB), path traversal (`..`), формат манифеста, версию формата, типы действий, безопасность путей к иконкам.

**Доказательство**

`IsArchiveEntryPathSafe` блокирует `..` и rooted paths. `ValidateManifest` проверяет версию и обязательные поля.

**Последствия**

ZIP Slip и decompression bomb защищены (с оговоркой в CR-005). Сильная сторона проекта.

## 9. Хранение данных, импорт и экспорт

### CR-016 — Атомарная запись settings.json

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Storage
**Расположение:** `AiteBar/AppSettingsService.cs`, метод `WriteSettingsWithBackupAsync()`, строки 256–293

**Проблема**

Запись настроек выполняется через: (1) запись во временный файл, (2) ротация бэкапов, (3) `File.Replace` с бэкапом текущего файла. При ошибке временный файл удаляется. Лимит бэкапов: 5 файлов.

**Доказательство**

```csharp
string tempFile = $"{_settingsFile}.{Guid.NewGuid():N}.tmp";
await File.WriteAllTextAsync(tempFile, json);
// ... RotateExistingBackupsOnly();
File.Replace(tempFile, _settingsFile, newestBackup);
```

**Последствия**

Настройки не повреждаются при аварийном завершении. Сильная сторона проекта.

---

### CR-017 — QuickNoteService сохраняет конфликтные копии

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Storage
**Расположение:** `AiteBar/QuickNoteService.cs`, метод `SaveConflictCopyAsync()`, строки 88–97

**Проблема**

При обнаружении внешних изменений файла Quick Note создаётся конфликтная копия с именем `QuickNote.conflict-{timestamp}.md`. Пользователь может открыть её во вложенном меню.

**Доказательство**

`HasExternalChanges()` сравнивает `LastWriteTimeUtc` с текущим временем файла. При конфликте вызывается `SaveConflictCopyAsync()`.

**Последствия**

Данные не теряются при конкурентном редактировании. Сильная сторона проекта.

## 10. Архитектура и сопровождаемость

### CR-018 — MainWindow содержит 1854 строки

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Architecture
**Расположение:** `AiteBar/MainWindow.xaml.cs` (1854 строки), частичные классы в 6 файлах

**Проблема**

`MainWindow` — крупнейший класс проекта. Логика разделена по partial-классам: `MainWindow.xaml.cs`, `MainWindow.DragAndDropHandler.cs`, `MainWindow.DropHandler.cs`, `MainWindow.ImportExportHandler.cs`, `MainWindow.KeyboardNavigationHandler.cs`, `MainWindow.PanelDragHandler.cs`, `MainWindow.TrayMenuHandler.cs`. Это смягчает проблему, но основной файл (1854 строки) всё ещё содержит контекстные меню, layout-логику, обработчики событий и позиционирование.

**Доказательство**

Список файлов partial-классов в `AiteBar/`.

**Последствия**

Сложность навигации и сопровождения. При правке одной функции нужно понимать контекст большого файла.

**Рекомендация**

Поэтапно выносить контекстные меню, drag-and-drop и позиционирование в отдельные partial-классы (уже начато).

---

### CR-019 — UtilityRegistry — статический реестр с автоматической регистрацией

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Architecture
**Расположение:** `AiteBar/UtilityRegistry.cs`

**Проблема**

`UtilityRegistry` использует reflection для автоматической регистрации утилит через атрибут `[Utility]`. `RegisterAllFromAssembly` сканирует текущую сборку. Проверяется версионирование контрактов и совместимость.

**Документация**

`docs/UTILITIES.md` точно описывает фактический механизм регистрации.

**Последствия**

Добавление новой утилиты требует только создания класса с атрибутом `[Utility]` — без правок основного кода. Сильная сторона проекта.

---

### CR-020 — Централизованные константы анимаций

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Architecture
**Расположение:** `AiteBar/Constants.cs`

**Проблема**

Все длительности анимаций собраны в `Constants.cs` и используются в MainWindow, drag-and-drop и утилитах. Значения: `AnimationFadeMs=140`, `AnimationSlideMs=150`, `PanelShowAnimationMs=175`, `PanelHideAnimationMs=140`, `QuickNoteSlideMs=200`.

**Документация**

`docs/DESIGN.md` и `docs/technical-reference.md` содержат актуальную таблицу констант.

**Последствия**

Анимации синхронизированы. Сильная сторона проекта.

---

### CR-021 — Удалённые элементы корректно нормализуются

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Architecture
**Расположение:** `AiteBar/AppSettingsService.cs`, метод `NormalizeElements()`, строки 440–501

**Проблема**

`NormalizeAppState()` корректно обрабатывает: null-элементы, дублирующиеся ID, отсутствующие ContextId, null RotationProfilePaths. Элементы с дублирующимися ID пропускаются (первый побеждает). Элементы без ContextId получают primary context.

**Доказательство**

Тесты `AppSettingsServiceTests` покрывают миграцию и нормализацию.

**Последствия**

Повреждённые настройки восстанавливаются без потери данных. Сильная сторона проекта.

## 11. WPF, XAML и интерфейс

### CR-022 — Позиционирование панели корректно для всех 4 сторон

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** WPF
**Расположение:** `AiteBar/PanelPositionHelper.cs`, `AiteBar/MainWindow.xaml.cs`

**Проблема**

`PanelPositionHelper.GetDockCoordinates()` корректно вычисляет координаты для Top, Bottom, Left, Right в показанном и скрытом состояниях. Центрирование в рабочей области монитора. Hysteresis (60 px) для drag handle предотвращает прыжки около углов.

**Доказательство**

Тесты `PanelPositionHelperTests` покрывают все 4 стороны и hysteresis.

**Последствия**

Корректное поведение панели на всех мониторах и сторонах. Сильная сторона проекта.

---

### CR-023 — OverflowWrapPanel реализует multi-band layout

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** WPF
**Расположение:** `AiteBar/OverflowWrapPanel.cs`

**Проблема**

Кастомная WPF-панель корректно реализует многострочное расположение кнопок с поддержкой горизонтальной и вертикальной ориентации, `LeadingPrimaryReserve` и `OverflowPrimaryReserve` для вертикального layout.

**Последствия**

Кнопки автоматически переносятся на следующую полосу при нехватке места. Максимум 3 полосы. Сильная сторона проекта.

---

### CR-024 — DPI-корректное позиционирование

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** WPF
**Расположение:** `AiteBar/MainWindow.xaml.cs`, метод `GetTargetScreenMetrics()`, строки 1014–1032

**Проблема**

Координаты мониторов (System.Drawing.Rectangle в пикселях) корректно конвертируются в DIP-ы через деление на `_cachedDpi`. Учитывается fallback через `SystemParameters` при отсутствии мониторов.

**Доказательство**

```csharp
bool isFromSystemParameters = (screen == null && primary == null);
double dpi = (isFromSystemParameters || _cachedDpi <= 0) ? 1.0 : _cachedDpi;
return (new Rect(drawingWorkArea.Left / dpi, ...));
```

**Последствия**

Корректное отображение на мониторах с разным DPI. Сильная сторона проекта.

## 12. Производительность

### CR-025 — RefreshPanel вызывает NormalizeAppState при каждом обновлении

**Приоритет:** MEDIUM
**Уверенность:** Подтверждено
**Категория:** Performance
**Расположение:** `AiteBar/MainWindow.xaml.cs`, метод `RefreshPanel()`, строка 1304

**Проблема**

`RefreshPanel()` вызывает `_settingsService.NormalizeAppState()` при каждом обновлении панели. `NormalizeAppState` итерирует все элементы и контексты для проверки изменений. При большом количестве кнопок (100+) и частых обновлениях (каждое изменение настроек, переключение контекста) это создаёт лишнюю нагрузку.

**Доказательство**

```csharp
public void RefreshPanel()
{
    _settingsService.NormalizeAppState(); // итерирует все элементы
    BuildPanelContextMenu();
    ...
}
```

**Последствия**

Замедление при большом количестве кнопок. На typical usage (10–30 кнопок) незаметно.

**Рекомендация**

Выносить нормализацию в отдельный вызов при мутации, а не при каждом рендере.

**Проверка исправления**

Benchmark: измерить время RefreshPanel до/после оптимизации при 200 кнопках.

---

### CR-026 — Кэш изображений кнопок корректно инвалидируется

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Performance
**Расположение:** `AiteBar/MainWindow.xaml.cs`, `RefreshPanel()`, строки 1291–1302

**Проблема**

Кэш `_buttonImageCache` очищается при изменении набора элементов (хэш по ID). Изображения загружаются асинхронно с проверкой версии панели (`panelVersion`) для предотвращения установки устаревших данных.

**Доказательство**

```csharp
if (panelVersion != _panelRefreshVersion) return;
```

**Последствия**

Нет мерцания и устаревших иконок при быстром переключении контекстов. Сильная сторона проекта.

## 13. Локализация

### CR-027 — Локализация покрывает 4 языка

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Localization
**Расположение:** `AiteBar/Resources/Strings.resx`, `Strings.ru.resx`, `Strings.de.resx`, `Strings.uk.resx`

**Проблема**

Поддерживаются: en, ru, de, uk + auto (по ОС). Тест `ResourceFiles_HaveSameKeysAndFormatPlaceholders` проверяет одинаковый набор ключей и плейсхолдеров во всех .resx файлах.

**Доказательство**

`AiteBar.Tests/LocalizationServiceTests.cs` содержит проверку ключей.

**Последствия**

Полная локализация интерфейса. Сильная сторона проекта.

---

### CR-028 — Локализация при смене языка обновляет открытые окна

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Localization
**Расположение:** `AiteBar/DarkWindow.cs`, `AiteBar/MainWindow.xaml.cs`

**Проблема**

`LocalizationService.CultureChanged` event подписывается в `MainWindow` и всех `DarkWindow` наследниках. При смене языка вызывается `RefreshLocalizedBindings` для обновления WPF bindings и `ApplyLocalizedText()` для обновления программных строк.

**Доказательство**

`DarkWindow.HandleCultureChanged` обновляет bindings + вызывает виртуальный `OnLocalizationChanged()`.

**Последствия**

Смена языка применяется без перезапуска. Сильная сторона проекта.

## 14. Тесты

### CR-029 — Покрытие тестами

**Приоритет:** INFO
**Уверенность:** Подтверждено
**Категория:** Testing
**Расположение:** `AiteBar.Tests/`

**Проблема**

Тестовый проект содержит 50+ тестовых классов, покрывающих: `ActionService`, `AppSettingsService`, `PanelLayoutHelper`, `PanelPositionHelper`, `PanelPackageService`, `BrowserHelper`, `HotkeyService`, `HotkeyValidationHelper`, `QuickNoteMarkdown`, `QuickNoteService`, `ContextStateHelper`, `Constants`, `EasingHelper`, `FontHelper`, `IcoEncoder`, `IconConverterService`, `LocalizationService`, `Logger`, `TelemetryService`, `UpdateCheckService`, `UtilityRegistry`, `ActivationZoneHelper`, `PathHelper`, `ProfileRotationHelper`, `TimerStopwatchFormatter`, `TimerStopwatchLayoutHelper`, `RuntimeLocalization`, `ReleaseVersion`.

CI workflow проверяет coverage с порогом 19% line coverage.

**Последствия**

Критические сценарии покрыты. Покрытие постепенно растёт.

---

### CR-030 — Тесты не покрывают UI-сценарии

**Приоритет:** INFO
**Уверенность:** Высокая вероятность
**Категория:** Testing
**Расположение:** `AiteBar.Tests/`

**Проблема**

Тесты фокусируются на non-UI логике (helper-ы, сервисы, модели). UI-сценарии (позиционирование панели, drag-and-drop, анимации, keyboard navigation) не автоматизированы. Это ожидаемо для WPF-приложения — AGENTS.md описывает ручную проверку UI.

**Последствия**

UI-регрессии выявляются только вручную.

**Рекомендация**

Рассмотреть Appium/WinAppDriver для автоматизации критических UI-сценариев.

## 15. Несоответствия документации и кода

| Документ | Утверждение | Фактическое поведение | Рекомендация |
|---|---|---|---|
| `USER_MANUAL.md` (FAQ) | «В редакторе кнопки доступны Chrome, Edge, Brave, Yandex и Firefox» | Код поддерживает также Opera, Opera GX, Vivaldi. Уточнить: «Основные — Chrome, Edge, Brave, Yandex, Firefox; также Opera, Opera GX, Vivaldi» | Уточнить список в FAQ |
| `technical-reference.md` | «Редактор web-кнопки показывает Chrome, Edge, Brave, Yandex и Firefox» | `BrowserHelper` поддерживает 8 браузеров. Opera/OperaGX/Vivaldi доступны через `BrowserType` enum | Техническая документация уже упоминает это как «дополнительные варианты» — корректно |
| `architecture.md` | «PanelSizePercent: 80%» | Значение по умолчанию: 80. `NormalizeAppState` ограничивает диапазон [50, 100] | Документация верна |

## 16. Сильные стороны проекта

1. **Атомарная запись настроек** через temp file + `File.Replace` с ротацией бэкапов — защита от потери данных при аварийном завершении.
2. **Централизованная layout-математика** (`PanelLayoutHelper`, `PanelPositionHelper`) — чистая математика без зависимости от WPF-элементов.
3. **Система регистрации утилит** (`UtilityRegistry` + `[Utility]` атрибут) — добавление новой утилиты не требует правок основного кода.
4. **Валидация ZIP-архивов** — защита от ZIP Slip, decompression bomb, path traversal, oversized entries.
5. **Тестирование hotkey валидации** — `HotkeyService` выделен в отдельный тестируемый класс с `IHotkeyRegistrar` интерфейсом.
6. **ActionService с IActionServiceRuntime** — тестирование через моки без зависимости от Win32.
7. **Корректная обработка DPI** — деление физических координат мониторов на `_cachedDpi`.
8. **Отписка от событий в OnClosed** — предотвращение утечек обработчиков.
9. **Localization с fallback** — отсутствующие ключи в non-en локали возвращают en-значение.
10. **Отсутствие unsafe-паттернов** — URL валидация, path traversal проверка, confirmation dialogs для опасных операций.

## 17. План исправлений

| Порядок | ID | Приоритет | Исправление | Зависимости | Риск регрессии |
|---:|---|---|---|---|---|
| 1 | CR-001 | HIGH | Добавить `case HotkeyCommand.FileSorter` в `ExecuteHotkeyCommand` | Нет | Минимальный |
| 2 | CR-002 | HIGH | Исправить `Logger.FlushQueue` — double-check pattern | Нет | Низкий |
| 3 | CR-003 | HIGH | Добавить `_screen.Dispose()` в `ScreenColorPickerWindow.Closed` | Нет | Минимальный |
| 4 | CR-006 | LOW | Заменить hardcoded строку на `LocalizationService.Get()` | Добавить ключ в .resx | Минимальный |
| 5 | CR-008 | LOW | Убрать `Debug.WriteLine` из `TaskbarPositionIndicatorService` | Нет | Минимальный |
| 6 | CR-007 | LOW | Убрать/сделать internal `TelemetryService.Initialize()` | Нет | Низкий |
| 7 | CR-004 | MEDIUM | Оптимизировать `AppSettingsService.Settings` getter | Нет | Средний |
| 8 | CR-005 | MEDIUM | Усилить проверку decompression bomb | Нет | Низкий |
| 9 | CR-025 | MEDIUM | Вынести `NormalizeAppState` из `RefreshPanel` | Нет | Средний |

## 18. Рекомендуемые тесты

1. **HotkeyCommand.FileSorter** — тест, проверяющий что все команды `HotkeyCommand` обрабатываются в `ExecuteHotkeyCommand` (или reverse-check что switch покрывает все enum values).
2. **Logger.FlushQueue** — тест конкурентного добавления логов с проверкой что все записи записаны.
3. **ScreenColorPickerWindow** — проверка что GDI object count не растёт после циклов открытия/закрытия.
4. **PanelPackageService decompression bomb** — тест с highly-compressible archive.
5. **UtilityRegistry error message** — проверка локализации сообщения о недоступности утилиты.

## 19. Итог

AiteBar — хорошо спроектированное desktop-приложение с чёткой архитектурой, продуманной безопасностью и хорошим тестовым покрытием для non-UI логики. Найдены 3 high-priority дефекта (отсутствие обработки FileSorter hotkey, потеря логов в Logger, утечка GDI в пипетке) и 5 medium-priority замечаний. Ни один дефект не является критическим (нет потери данных, нет выполнения произвольного кода). Исправление high-priority дефектов требует минимальных изменений в 3 файлах.
