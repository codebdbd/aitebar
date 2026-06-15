# AiteBar — аудит лучших практик

## 1. Резюме

### Общее состояние проекта

AiteBar — качественно реализованное WPF desktop-приложение для Windows. Кодовая база (~60 исходных файлов, ~45 тестовых) показывает зрелую архитектуру с чётким разделением сервисов, helper-классов и UI. Проект имеет хорошую документацию, локализацию на 4 языка, систему резервных копий настроек и продуманный pipeline сборки инсталлятора.

### Главные сильные стороны

- Чистая математика layout (`PanelLayoutHelper`) — чисто функциональная, хорошо протестирована
- Система backup/restore настроек с атомарной записью через temp file + File.Replace
- Универсальная система утилит (`IUtility`, `UtilityBase<TWindow>`, `UtilityRegistry`)
- Тестируемость non-UI логики через интерфейсы (`IActionServiceRuntime`, `IHotkeyRegistrar`, `IQuickNoteProcessStartDispatcher`)
- 45+ тестовых файлов с хорошей покрываемостью критических компонентов
- Централизованная локализация с runtime culture switching
- Валидация опасных команд (`ContainsPotentiallyDangerousCommandSyntax`)
- Ограничения размера пакетов импорта/экспорта

### Количество замечаний по приоритетам

| Приоритет | Количество |
|-----------|-----------|
| Critical | 3 |
| High | 8 |
| Medium | 15 |
| Low | 10 |
| Info | 6 |
| **Итого** | **42** |

### Пять наиболее важных проблем

1. **BP-001**: `DispatcherUnhandledException.Handled = false` — необработанные исключения в UI-потоке убивают процесс без graceful shutdown
2. **BP-002**: Data race в `_appSettings` — объект читается из UI-потока и модифицируется из background-потока без синхронизации
3. **BP-003**: `MainWindow` — God Class (~2776 строк) с 15+ ответственностями
4. **BP-004**: `NativeIntegrationService` — mouse hook не unhooked при异常ном завершении (нет destructor/finalizer)
5. **BP-005**: Отсутствие GitHub Actions workflow — нет CI/CD, нет автоматических тестов при push/PR

### Общий вывод

Проект находится в хорошем состоянии для desktop-приложения такого масштаба. Основные риски — data race в AppSettings, отсутствие CI/CD, и чрезмерная размерность MainWindow. Рекомендации направлены на снижение этих рисков без масштабного рефакторинга.

---

## 2. Область и методика анализа

### Изученные файлы и каталоги

- Весь каталог `AiteBar/` (~60 .cs файлов, 16 .xaml файлов)
- Каталог `AiteBar.Tests/` (~45 тестовых файлов)
- `docs/`: architecture.md, DESIGN.md, functions.md, technical-reference.md, USER_MANUAL.md, UTILITIES.md
- `installer/`: AiteBar.iss, Build-Installer.ps1
- `AiteBar.csproj`, `AiteBar.Tests.csproj`, `Directory.Build.props`, `AiteBar.sln`
- `AGENTS.md`, `README.md`, `CHANGELOG.md`

### Выполненные команды

| Команда | Результат | Детали |
|---------|-----------|--------|
| `dotnet restore` | **OK** | Пакеты восстановлены |
| `dotnet build -c Release` | **OK** | 0 ошибок, 0 предупреждений (13.34s) |
| `dotnet test -c Release` | **OK** | 484 пройдено, 0 не пройдено, 0 пропущено (9s) |
| `dotnet format --verify-no-changes` | **Ошибка** | ~250+ ошибок форматирования (whitespace + end-of-line) |
| `dotnet list package --outdated` | **OK** | 2 устаревших пакета |
| `dotnet list package --vulnerable` | **OK** | 0 уязвимых пакетов |

### Результаты проверок

**Сборка:** Release собирается без ошибок и предупреждений.

**Тесты:** Все 484 теста проходят.

**Форматирование:** `dotnet format` обнаружил ~250+ ошибок:
- **Whitespace** (неправильные отступы): `ActionService.cs` (12 ошибок), `App.xaml.cs` (3 ошибки), `AppSettingsService.cs` (~100 ошибок — смешанные отступы в `NormalizeAppState`, `NormalizeElements`, `AreElementsEquivalent`), `IconConverterService.cs` (1 ошибка), `Logger.cs` (~15 ошибок — смешанные отступы в `RotateLogFile`)
- **End-of-line** (LF вместо CRLF): `RuntimeLocalizationInfrastructureTests.cs`, `RuntimeLocalizationWindowSourceTests.cs`, `UpdateCheckServiceTests.cs`, `WpfTestCollection.cs` — все используют LF вместо CRLF

**Зависимости:**
- Устаревшие: `SkiaSharp 3.119.2 → 3.119.4`, `SkiaSharp.NativeAssets.Win32 3.119.2 → 3.119.4`
- Уязвимости: не обнаружены

**CI/CD:** GitHub Actions workflow отсутствуют.

---

## 3. Фактическая архитектура

### Основные модули

```
AiteBar (WinExe)
├── UI Layer
│   ├── MainWindow.xaml.cs (2776 строк) — основная панель
│   ├── SettingsWindow.xaml.cs (853 строки) — редактирование кнопки
│   ├── AppSettingsWindow.xaml.cs — настройки программы
│   ├── QuickNoteWindow.xaml.cs (1019 строк) — быстрые заметки
│   ├── TimerStopwatchWindow.xaml.cs — таймер/секундомер
│   ├── FileSorterWindow.xaml.cs — сортировщик файлов
│   ├── IconConverterWindow.xaml.cs — конвертер иконок
│   ├── IconPickerWindow.xaml.cs — выбор иконки
│   ├── ScreenColorPickerWindow.cs — пипетка цвета
│   ├── DarkWindow.cs — базовый класс тёмных окон
│   ├── DarkDialog.xaml.cs — диалог подтверждения
│   ├── TextPromptDialog.xaml.cs — ввод текста
│   ├── RotationProfileSelectionWindow.xaml.cs — выбор профилей
│   └── AboutWindow.xaml.cs — о программе
├── Services Layer
│   ├── ActionService.cs (561 строк) — выполнение действий
│   ├── AppSettingsService.cs (645 строк) — управление настройками
│   ├── PanelPackageService.cs (392 строки) — импорт/экспорт
│   ├── QuickNoteService.cs (132 строки) — заметки
│   ├── NativeIntegrationService.cs (67 строк) — mouse hook
│   ├── LocalizationService.cs (230 строк) — локализация
│   ├── HotkeyService.cs (237 строк) — горячие клавиши
│   ├── UpdateCheckService.cs (236 строк) — проверка обновлений
│   ├── TelemetryService.cs (216 строк) — Sentry
│   └── UnifiedButtonService.cs — unified button list
├── Helpers Layer
│   ├── PanelLayoutHelper.cs (262 строки) — расчёт layout
│   ├── PanelPositionHelper.cs — расчёт координат
│   ├── ContextStateHelper.cs — контексты
│   ├── BrowserHelper.cs (256 строк) — браузеры
│   ├── PathHelper.cs (26 строк) — пути
│   ├── ActionTargetHelper.cs — валидация целей
│   ├── FontHelper.cs — шрифты
│   ├── IconHelper.cs (103 строки) — иконки
│   ├── ProfileRotationHelper.cs — ротация профилей
│   ├── QuickNoteMarkdown.cs — парсинг Markdown
│   ├── QuickNoteLayoutHelper.cs — layout заметок
│   ├── QuickNoteDocumentHelper.cs — документ
│   ├── QuickNoteTheme.cs — темы заметок
│   ├── EasingHelper.cs — функции анимаций
│   ├── ActivationZoneHelper.cs — зона активации
│   ├── HotkeyValidationHelper.cs — валидация hotkeys
│   ├── HotkeyKeyCatalog.cs — каталог клавиш
│   ├── PanelPackageMapper.cs — маппинг пакетов
│   ├── PanelPackageManifest.cs — manifest
│   ├── FileSorterService.cs — сортировка файлов
│   ├── FileSorterUtility.cs — утилита сортировки
│   ├── IconConverterService.cs — конвертация иконок
│   ├── IconConverterUtility.cs — утилита конвертации
│   ├── IconConverterModels.cs — модели конвертера
│   ├── IcoEncoder.cs — кодировщик ICO
│   ├── TimerStopwatchUtility.cs — утилита таймера
│   ├── TimerStopwatchFormatter.cs — форматирование
│   ├── TimerStopwatchLayoutHelper.cs — layout таймера
│   ├── ColorPickerUtility.cs — пипетка
│   ├── QuickNoteUtility.cs — заметки
│   ├── AppSettingsService.cs — настройки
│   └── UtilityRegistry.cs — реестр утилит
├── Models
│   ├── Models.cs (171 строка) — AppSettings, CustomElement, PanelContext
│   └── ActionExecutionResult.cs
├── Native
│   ├── NativeMethods.cs (127 строк) — P/Invoke
│   └── NativeIntegrationService.cs — mouse hook
├── Resources
│   ├── Strings.resx, Strings.ru.resx, Strings.uk.resx, Strings.de.resx
│   └── Шрифты, иконки
└── Infrastructure
    ├── App.xaml.cs (106 строк) — точка входа
    ├── Logger.cs (80 строк) — логирование
    └── Constants.cs (22 строки) — константы анимаций
```

### Зависимости

- **MainWindow** зависит от: ActionService, AppSettingsService, PanelPackageService, NativeIntegrationService, HotkeyService, UnifiedButtonService, LocalizationService
- **SettingsWindow** зависит от: MainWindow (для GetContextsSnapshot, GetAppSettings)
- **AppSettingsService** — автономный, зависит от PathHelper, ContextStateHelper
- **ActionService** — зависит от AppSettingsService, BrowserHelper, NativeMethods, UtilityRegistry

### Точки высокой связанности

1. `MainWindow.xaml.cs` — центральная точка, связанность с 6+ сервисами
2. `SettingsWindow` → `MainWindow` — прямая зависимость от concrete класса
3. `AppSettingsService` → `_appSettings` — мутабельный singleton-like объект

### Критические потоки выполнения

1. Запуск: `App.OnStartup` → `LoadSettingsAndApplyCultureAsync` → `MainWindow` constructor → `Window_Loaded` → `EnsureStartupInfrastructure` → `RefreshPanel`
2. Показ панели: Timer tick → `ActivationZoneHelper.IsInActivationZone` → `ShowDock` → `Toggle(false)` → анимация → `PositionWindowImmediately`
3. Выполнение действия: Button click → `ExecuteUnifiedButtonActionAsync` → `ActionService.ExecuteCustomActionAsync` → switch по ActionType

### Расхождения с architecture.md

- `architecture.md` утверждает `MaxUserBands = 2`, но `PanelLayoutHelper.cs:12` содержит `MaxUserBands = 3`
- `architecture.md` описывает `_quickNoteWindow` и `_timerStopwatchWindow` как поля `ActionService`, но фактически эти окна создаются через `UtilityRegistry` и `UtilityBase<TWindow>`, а в `ActionService` нет таких полей

---

## 4. Сильные стороны

### 4.1 Тестируемость non-UI логики

**Доказательство:** `ActionService` принимает `IActionServiceRuntime` через constructor injection. `HotkeyService` принимает `IHotkeyRegistrar`. `QuickNoteService` принимает `IQuickNoteProcessStartDispatcher`. Это позволяет тестировать бизнес-логику без WPF.

**Подтверждение:** `AiteBar.Tests/ActionServiceTests.cs`, `AiteBar.Tests/HotkeyServiceTests.cs`, `AiteBar.Tests/QuickNoteServiceTests.cs`

### 4.2 Атомарное сохранение настроек

**Доказательство:** `AppSettingsService.WriteSettingsWithBackupAsync` записывает во временный файл, затем делает `File.Replace` с backup. При crash во время записи основной файл остаётся нетронутым.

**Подтверждение:** `AiteBar/AppSettingsService.cs:214-251`

### 4.3 Backup/restore настроек

**Доказательство:** 5-level backup rotation (`MaxBackupCount = 5`), автоматическое восстановление из backup при corrupt main file (`TryLoadFromBackup`).

**Подтверждение:** `AiteBar/AppSettingsService.cs:112-187`

### 4.4 Валидация опасных команд

**Доказательство:** `ContainsPotentiallyDangerousCommandSyntax` проверяет shell chaining (`&`, `|`, `>`, `<`) и потенциально разрушительные команды (`del`, `format`, `shutdown`, `diskpart` и др.). Предупреждение добавляется в confirmation dialog.

**Подтверждение:** `AiteBar/ActionService.cs:287-306`

### 4.5 Безопасный URL validation

**Доказательство:** `OpenUrl` валидирует что URI — `http` или `https`. `UpdateCheckService.GetTrustedGitHubUrl` проверяет host (`github.com`) и path prefix (`/codebdbd/aitebar/`). `TryGetDropTarget` валидирует URL через `Uri.TryCreate`.

**Подтверждение:** `AiteBar/MainWindow.xaml.cs:1039-1051`, `AiteBar/UpdateCheckService.cs:188-209`

### 4.6 Ограничения размеров импорта

**Доказательство:** `MaxPackageFileBytes = 25MB`, `MaxManifestBytes = 2MB`, `MaxPackageEntryBytes = 10MB`, `MaxPackageUncompressedBytes = 50MB`, `MaxPackageEntryCount = 256`. `ValidateArchiveEntrySizes` проверяет каждый entry.

**Подтверждение:** `AiteBar/PanelPackageService.cs:18-22`

### 4.7 Централизованные константы анимаций

**Доказательство:** `Constants.cs` содержит все длительности анимаций. Все классы используют эти константы вместо магических значений.

**Подтверждение:** `AiteBar/Constants.cs`

### 4.8 Mutex для single instance

**Доказательство:** `App.xaml.cs` создаёт именованный Mutex (`Global\AiteBar_Mutex_Unique_String_123`). Inno Setup использует тот же Mutex для close/restart applications.

**Подтверждение:** `AiteBar/App.xaml.cs:26-37`, `installer/AiteBar.iss:35`

### 4.9 Идиоматичное использование ArgumentList

**Доказательство:** `ActionService.CreateScriptProcessStartInfo` и `BuildWebActionProcessStartInfo` используют `psi.ArgumentList.Add()` вместо конкатенации строк, что предотвращает command injection через аргументы.

**Подтверждение:** `AiteBar/ActionService.cs:397-451`

### 4.10 Хорошая локализация

**Доказательство:** 4 языка (en, ru, uk, de), runtime culture switching, `LocExtension` для XAML bindings, fallback на English. Тест `ResourceFiles_HaveSameKeysAndFormatPlaceholders` требует одинаковых ключей.

**Подтверждение:** `AiteBar/LocalizationService.cs`, `AiteBar.Tests/LocalizationServiceTests.cs`

---

## 5. Реестр замечаний

| ID | Приоритет | Категория | Краткое описание | Уверенность | Стоимость |
|----|-----------|-----------|-----------------|-------------|-----------|
| BP-001 | Critical | Reliability | `DispatcherUnhandledException.Handled = false` убивает процесс | Confirmed | XS |
| BP-002 | Critical | Concurrency | Data race в `_appSettings` между UI и background потоками | Probable | M |
| BP-003 | High | Architecture | `MainWindow` — God Class (2776 строк, 15+ ответственности) | Confirmed | L |
| BP-004 | Critical | Native | `NativeIntegrationService` — нет GC.SuppressFinalize, hook может остаться | Probable | S |
| BP-005 | High | CI/CD | GitHub Actions workflow отсутствует | Confirmed | M |
| BP-006 | High | Concurrency | `DragHandle_MouseLeftButtonUp` дублирует `DragHandle_LostMouseCapture` | Confirmed | S |
| BP-007 | High | Concurrency | `SettingsWindow` напрямую зависит от `MainWindow` (tight coupling) | Confirmed | M |
| BP-008 | High | Reliability | `TelemetryService.LoadSettingsFromFile` — `Thread.Sleep` блокирует поток | Confirmed | XS |
| BP-009 | High | Architecture | `ActivateContextRelative` / `ActivateContextByIndex` / `ActivateContextById` — дублирование логики | Confirmed | S |
| BP-010 | High | Concurrency | Fire-and-forget `SaveAsync` без отображения ошибки пользователю | Confirmed | S |
| BP-011 | High | Security | `ActionService.ExecuteCommand` — передаёт строку в `cmd.exe /c` без дополнительного quoting | Probable | S |
| BP-012 | High | Security | `IconHelper.DownloadFaviconAsync` — HTTP downgrade возможен при redirect | Probable | XS |
| BP-013 | Medium | Reliability | `Logger.RotateLogFile` — swallowed exception, потеря логов | Confirmed | XS |
| BP-014 | Medium | Concurrency | `_saveSemaphore` в `AppSettingsService` не защищает чтение `_appSettings` | Confirmed | S |
| BP-015 | Medium | Configuration | `MaxSettingsFileBytes = 100MB` — слишком много для JSON settings | Confirmed | XS |
| BP-016 | Medium | Architecture | `SettingsWindow.LoadProfilesAsync` — fire-and-forget с `t.Exception!` | Confirmed | XS |
| BP-017 | Medium | Reliability | `BuildDuplicateElementName` — потенциально бесконечный цикл | Probable | XS |
| BP-018 | Medium | Performance | `_buttonImageCache` — brush-конвертация на каждый refresh без кэширования | Confirmed | XS |
| BP-019 | Medium | Security | `BrowserHelper.GetPathFromRegistry` — swallowed exceptions без логирования | Confirmed | XS |
| BP-020 | Medium | Architecture | `_currentUnifiedButtons` и `_activeContextElements` — неиспользуемые поля | Confirmed | XS |
| BP-021 | Medium | Documentation | `architecture.md` указывает `MaxUserBands = 2`, фактически `3` | Confirmed | XS |
| BP-022 | Medium | Documentation | `architecture.md` описывает `_quickNoteWindow` в `ActionService`, фактически нет | Confirmed | XS |
| BP-023 | Medium | Reliability | `TelemetryService.Flush` — `ContinueWith` может не дождаться завершения | Confirmed | XS |
| BP-024 | Medium | Security | `PanelPackageService` — `ZipFile.ExtractToDirectory` без проверки path traversal | Probable | S |
| BP-025 | Medium | Architecture | `Window_Loaded` — `_ = CompleteDeferredStartupAsync()` fire-and-forget | Confirmed | XS |
| BP-026 | Medium | Concurrency | `_startupCts.Dispose()` в `finally` block `CompleteDeferredStartupAsync` | Confirmed | XS |
| BP-027 | Medium | Reliability | `GetTargetScreen` — fallback на `Screen.PrimaryScreen` может вернуть null | Probable | XS |
| BP-028 | Low | Code Quality | `SettingsWindow` — 853 строки, смешивает UI и бизнес-логику | Confirmed | M |
| BP-029 | Low | Code Quality | `QuickNoteWindow` — 1019 строк, complex state management | Confirmed | M |
| BP-030 | Low | Concurrency | `MainWindow` — 30+ приватных полей для UI state | Confirmed | S |
| BP-031 | Low | Performance | `_timer` (DispatcherTimer 30ms) — частое опросание позиции курсора | Confirmed | XS |
| BP-032 | Low | Security | `ExecuteCommand` — передаёт пользовательскую строку в cmd.exe | Confirmed | S |
| BP-033 | Low | Reliability | `Logger.BuildLogEntry` — заменяет `\n` на ` | ` — теряется структура стека | Confirmed | XS |
| BP-034 | Low | Architecture | `CreateMenuItem` — создаёт `TextBlock` для каждого glyph, нет пулинга | Confirmed | XS |
| BP-035 | Low | Documentation | `USER_MANUAL.md` — TODO вставки скриншотов | Confirmed | XS |
| BP-036 | Low | Build | `AllowUnsafeBlocks=true` в .csproj — не используется | Probable | XS |
| BP-037 | Low | Build | `GenerateAssemblyInfo=false` — ручное управление AssemblyInfo | Info | XS |
| BP-038 | Info | Architecture | Нет DI-контейнера — оправдано для desktop-приложения | Confirmed | — |
| BP-039 | Info | Architecture | Code-behind вместо MVVM — оправдано для WPF desktop-приложения | Confirmed | — |
| BP-040 | Info | Build | `Directory.Build.props` — `Deterministic=true`, `RestorePackagesWithLockFile=true` | Confirmed | — |
| BP-041 | Info | Security | Sentry отключен по умолчанию, включение через env vars | Confirmed | — |
| BP-042 | Medium | Code Quality | `dotnet format` — ~250+ ошибок whitespace и end-of-line | Confirmed | S |

---

### BP-001 — DispatcherUnhandledException.Handled = false

**Приоритет:** Critical
**Категория:** Reliability
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

В `App.xaml.cs:99-104`, `DispatcherUnhandledException` обработчик устанавливает `args.Handled = false`. Это означает, что любое необработанное исключение в UI-потоке немедленно завершит процесс. При этом:
- `NotifyIcon` не будет disposed (иконка останется в tray)
- Mouse hook не будет unhooked (системный hook останется активным до перезагрузки)
- Hotkeys не будут unregistered
- Sentry flush не будет выполнен

**Подтверждение**
- `AiteBar/App.xaml.cs:99-104`

**Почему это важно**

Пользователь столкнётся с «зависшей» иконкой в tray и потенциально активным hook, которые нельзя снять без перезагрузки системы или убийства процесса через диспетчер задач.

**Рекомендация**

```csharp
DispatcherUnhandledException += (_, args) =>
{
    TelemetryService.CaptureException(args.Exception, "dispatcher_unhandled");
    TelemetryService.Flush(TimeSpan.FromSeconds(2));
    args.Handled = true; // Graceful recovery
};
```

**Критерии готовности**

- После воспроизведения unhandled exception в UI, приложение продолжает работать
- Иконка tray корректно удаляется при закрытии

**Необходимые тесты**

- Интеграционный тест: вызов `throw new InvalidOperationException()` в UI-потоке, проверка что приложение не завершается

---

### BP-002 — Data race в _appSettings

**Приоритет:** Critical
**Категория:** Concurrency
**Уверенность:** Probable
**Стоимость:** M
**Риск изменения:** Medium

**Проблема**

`_appSettings` в `AppSettingsService` — мутабельный объект, который:
- Читается из UI-потока через `Settings` property (без lock)
- Модифицируется в `NormalizeAppState()` из UI-потока
- Десериализуется в `LoadAsync()` из background-потока
- Частично защищён `_saveSemaphore` в `SaveAsync()`, но чтение не синхронизировано

Типичный race condition: `RefreshPanel()` вызывает `NormalizeAppState()` на UI-потоке, пока `LoadAsync()` десериализует объект в `Task.Run`.

**Подтверждение**
- `AiteBar/AppSettingsService.cs:31` — `Settings` property возвращает `_appSettings` напрямую
- `AiteBar/AppSettingsService.cs:43-110` — `LoadAsync()` модифицирует `_appSettings`
- `AiteBar/AppSettingsService.cs:189-212` — `SaveAsync()` читает `_appSettings`
- `AiteBar/MainWindow.xaml.cs:1415` — `_settingsService.NormalizeAppState()` в `RefreshPanel()`

**Почему это важно**

Data race может привести к corrupted state, lost updates, или `NullReferenceException` при одновременном чтении и записи свойств `_appSettings`.

**Рекомендация**

Добавить `lock(_stateLock)` в `Settings` property getter и в `NormalizeAppState()`. Или ввести immutable snapshot pattern.

**Критерии готовности**

- Нет `NullReferenceException` при параллельных вызовах `LoadAsync()` и `RefreshPanel()`
- Тест: параллельный вызов `SaveAsync()` и `Elements` getter в 10 потоках

**Необходимые тесты**

- Concurrent access test для `AppSettingsService.Settings` и `Elements`

---

### BP-003 — MainWindow God Class

**Приоритет:** High
**Категория:** Architecture
**Уверенность:** Confirmed
**Стоимость:** L
**Риск изменения:** High

**Проблема**

`MainWindow.xaml.cs` — 2776 строк, 30+ приватных полей, 50+ методов. Ответственности:
- Показ/скрытие панели с анимацией
- Drag-and-drop reorder кнопок
- Drag-handle для смены стороны
- Контекстные меню (panel, element, utility)
- Keyboard navigation
- Tray icon management
- Mouse hook integration
- Panel layout calculation delegation
- Import/Export
- Button image loading и кэширование
- Context switching
- DPI-awareness

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs` — 2776 строк, ~30 приватных полей

**Почему это важно**

Высокая сложность файла затрудняет:
- Нахождение багов
- Добавление новых функций
- Написание тестов
- Онбординг новых разработчиков

**Рекомендация**

Безопасные границы декомпозиции (без изменения поведения):
1. **KeyboardNavigationService** — `Window_PreviewKeyDown`, `EnablePanelKeyboardMode`, `FocusPanelForKeyboard`, `GetAllFocusableButtons` (~120 строк)
2. **TrayIconService** — `InitTrayIcon`, `ShowTrayContextMenu` (~80 строк)
3. **PanelDragDropService** — drag-and-drop reorder логика (~150 строк)

Не нужно выносить:
- `ShowDock/HideDock/ToggleDock` — тесно связаны с lifecycle окна
- `RefreshPanel` — центральная точка, должна оставаться в MainWindow
- `UpdateOrientation` — связан с XAML элементами

**Критерии готовности**

- Все существующие тесты проходят
- Manual test: показ/скрытие, drag-and-drop, keyboard nav, tray — всё работает

**Необходимые тесты**

- Все существующие тесты + integration test для нового service

---

### BP-004 — NativeIntegrationService без финализатора

**Приоритет:** Critical
**Категория:** Native
**Уверенность:** Probable
**Стоимость:** S
**Риск изменения:** Low

**Проблема**

`NativeIntegrationService` устанавливает low-level mouse hook через `SetWindowsHookEx`. Если `Dispose()` не вызовется (crash, exception в UI-потоке), hook останется активным в системе до перезагрузки.

Нет `~NativeIntegrationService()` (finalizer) и `GC.SuppressFinalize()`.

**Подтверждение**
- `AiteBar/NativeIntegrationService.cs:22-36` — `InstallMouseHook`
- `AiteBar/NativeIntegrationService.cs:38-43` — `UninstallMouseHook`
- `AiteBar/NativeIntegrationService.cs:62-66` — `Dispose` (без finalizer)

**Почему это важно**

Активный low-level mouse hook потребляет системные ресурсы и может вызвать performance degradation в других приложениях.

**Рекомендация**

Добавить destructor и `GC.SuppressFinalize`:

```csharp
~NativeIntegrationService()
{
    UninstallMouseHook();
}

public void Dispose()
{
    UninstallMouseHook();
    GC.SuppressFinalize(this);
}
```

**Критерии готовности**

- После crash приложения hook автоматически снимается
- `Dispose()` вызывается в `MainWindow.OnClosed`

**Необходимые тесты**

- Manual: завершить процесс через Task Manager, проверить что hook снят

---

### BP-005 — Отсутствие GitHub Actions

**Приоритет:** High
**Категория:** CI/CD
**Уверенность:** Confirmed
**Стоимость:** M
**Риск изменения:** Low

**Проблема**

В репозитории нет `.github/workflows/`. AGENTS.md описывает workflows (`.github/workflows/build-test.yml`, `.github/workflows/codeql.yml`, `.github/dependabot.yml`), но файлы отсутствуют.

**Подтверждение**
- `ls D:\01_Codebdbd\01_projects\mino\aitebar\.github` — directory does not exist
- `AGENTS.md` содержит описание workflows

**Почему это важно**

Без CI/CD:
- Нет автоматической проверки при push/PR
- Нет coverage reports
- Нет автоматического обновления зависимостей (Dependabot)
- Нет CodeQL security analysis

**Рекомендация**

Создать минимальный workflow:
1. `build-test.yml` — build + test на push/PR в main
2. `codeql.yml` — CodeQL analysis
3. `dependabot.yml` — auto-update NuGet

**Критерии готовности**

- Workflow выполняется при push
- Tests проходят в CI
- Coverage публикуется

**Необходимые тесты**

- CI smoke test: push → workflow runs → tests pass

---

### BP-006 — DragHandle double event handling

**Приоритет:** High
**Категория:** Concurrency
**Уверенность:** Confirmed
**Стоимость:** S
**Риск изменения:** Medium

**Проблема**

`DragHandle_MouseLeftButtonUp` и `DragHandle_LostMouseCapture` содержат идентичную логику завершения drag:
- Сброс `_isPanelDragging`
- `SetDragHandleActive(false)`
- `SetPanelDragRenderingActive(false)`
- Сохранение или откат `Edge`/`MonitorIndex`

При завершении перетаскивания WPF вызывает оба метода. Порядок вызовов зависит от WPF implementation.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:1948-1968` — `DragHandle_LostMouseCapture`
- `AiteBar/MainWindow.xaml.cs:2000-2023` — `DragHandle_MouseLeftButtonUp`

**Почему это важно**

Двойное выполнение логики может привести к двойному `SaveAsync()` или race condition при откате Edge/MonitorIndex.

**Рекомендация**

Оставить только `DragHandle_LostMouseCapture` как единую точку завершения drag. `MouseLeftButtonUp` может просто вызвать `DragHandle.ReleaseMouseCapture()`.

**Критерии готовности**

- Drag-and-drop handle работает корректно на всех 4 сторонах
- SaveAsync вызывается ровно один раз при изменении

**Необходимые тесты**

- Manual: перетаскивание панели на каждый из 4 краёв, проверка что Edge и MonitorIndex сохраняются

---

### BP-007 — SettingsWindow → MainWindow tight coupling

**Приоритет:** High
**Категория:** Architecture
**Уверенность:** Confirmed
**Стоимость:** M
**Риск изменения:** Medium

**Проблема**

`SettingsWindow` напрямую зависит от `MainWindow`:

```csharp
private readonly MainWindow _mainWindow;
```

Использует: `GetContextsSnapshot()`, `GetAppSettings()`, `GetActionService()`. Это нарушает SRP и затрудняет тестирование.

**Подтверждение**
- `AiteBar/SettingsWindow.xaml.cs:34`

**Почему это важно**

Невозможно протестировать `SettingsWindow` без `MainWindow`. Невозможно переиспользовать `SettingsWindow` в другом контексте.

**Рекомендация**

Ввести интерфейс `ISettingsWindowContext`:

```csharp
internal interface ISettingsWindowContext
{
    IReadOnlyList<PanelContext> GetContextsSnapshot();
    AppSettings GetAppSettings();
    ActionService GetActionService();
}
```

`MainWindow` реализует интерфейс, `SettingsWindow` принимает интерфейс.

**Критерии готовности**

- `SettingsWindow` не ссылается на `MainWindow`
- Все существующие сценарии работают

**Необходимые тесты**

- Unit test: `SettingsWindow` с mock `ISettingsWindowContext`

---

### BP-008 — TelemetryService.Thread.Sleep

**Приоритет:** High
**Категория:** Reliability
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`TelemetryService.LoadSettingsFromFile` (строка 109) использует `Thread.Sleep(100 * (1 << attempt))` для retry. Это блокирующий вызов, который может выполняться на UI-потоке.

**Подтверждение**
- `AiteBar/TelemetryService.cs:99-116`

**Почему это важно**

`Thread.Sleep` замораживает поток на 100-400ms, что может вызвать UI freeze.

**Рекомендация**

Заменить на `await Task.Delay()` или вынести в background thread.

**Критерии готовности**

- Нет UI freeze при чтении настроек Sentry

**Необходимые тесты**

- Manual: запуск приложения, проверка отсутствия freeze

---

### BP-009 — Дублирование логики context switching

**Приоритет:** High
**Категория:** Architecture
**Уверенность:** Confirmed
**Стоимость:** S
**Риск изменения:** Medium

**Проблема**

4 метода с частично дублирующейся логикой:
- `SwitchActiveContextAsync` — async, с `SaveAsync`
- `ActivateContextRelative` — sync, fire-and-forget save
- `ActivateContextByIndex` — sync, fire-and-forget save
- `ActivateContextById` — sync, fire-and-forget save

Каждый содержит: получение enabled contexts, поиск текущего индекса, вычисление следующего, проверку на equality, обновление `ActiveContextId`, `_pendingContextAnimationDirection`, `RefreshPanel`.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:277-328` — `SwitchActiveContextAsync`, `ActivateContextRelative`, `ActivateContextByIndex`
- `AiteBar/MainWindow.xaml.cs:639-662` — `ActivateContextById`

**Почему это важно**

Любое изменение логики переключения контекста требует правки в 4 местах. Риск расхождения поведения.

**Рекомендация**

Объединить в один приватный метод `SwitchContextCore(string nextContextId)`, остальные методы вызывают его.

**Критерии готовности**

- Все 4 способа переключения контекста работают одинаково
- Save вызывается корректно

**Необходимые тесты**

- Unit test: `SwitchContextCore` с mock

---

### BP-010 — Fire-and-forget SaveAsync без error reporting

**Приоритет:** High
**Категория:** Concurrency
**Уверенность:** Confirmed
**Стоимость:** S
**Риск изменения:** Low

**Проблема**

В 5+ местах используется:

```csharp
_ = _settingsService.SaveAsync().ContinueWith(
    t => { if (t.Exception != null) Logger.Log(t.Exception); },
    TaskContinuationOptions.OnlyOnFaulted);
```

Если save упадёт, ошибка логируется, но пользователь не узнает. Контекст переключился, но настройки не сохранились.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:310`, `327`, `661`, `961`, `1961`

**Почему это важно**

Пользователь может потерять изменения настроек без уведомления.

**Рекомендация**

Добавить toast/notification для критических save failures или использовать `async void` с try-catch и показом dialog.

**Критерии готовности**

- При ошибке save пользователь видит уведомление

**Необходимые тесты**

- Unit test: `SaveAsync` возвращает faulted task, проверка что ошибка отображается

---

### BP-011 — ExecuteCommand без дополнительного quoting

**Приоритет:** High
**Категория:** Security
**Уверенность:** Probable
**Стоимость:** S
**Риск изменения:** Medium

**Проблема**

`ActionService.ExecuteCommand` передаёт пользовательскую строку в `cmd.exe /c {command}`. Если команда содержит пробелы или кавычки, `cmd.exe` может интерпретировать их непредсказуемо.

**Подтверждение**
- `AiteBar/ActionService.cs:263-274`

**Почему это wichtig**

Пользователь может случайно (или намеренно) передать команду, которая выполнится непредсказуемо из-за проблем с quoting.

**Рекомендация**

Документировать что команда передаётся как-is в `cmd.exe /c`. Учитывать что `ArgumentList` не используется для `cmd.exe /c` — строка передаётся в `Arguments`.

**Критерии готовности**

- Документировано поведение quoting
- Тест: команда с пробелами и кавычками

**Необходимые тесты**

- Unit test: `ContainsPotentiallyDangerousCommandSyntax` для edge cases

---

### BP-012 — HTTP downgrade при favicon redirect

**Приоритет:** High
**Категория:** Security
**Уверенность:** Probable
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`IconHelper.DownloadFaviconAsync` использует `HttpClient` без `AllowAutoRedirect = false`. При redirect с HTTPS на HTTP данные могут быть перехвачены (MITM).

**Подтверждение**
- `AiteBar/IconHelper.cs:17-43`

**Почему это важно**

Хотя это не критическая уязвимость (загружается только favicon), это нарушает best practices для HTTPS-only communication.

**Рекомендация**

Проверять что最终ный URL остаётся HTTPS, или отключать auto-redirect и обрабатывать redirect вручную.

**Критерии готовности**

- Favicon загружается только по HTTPS

**Необходимые тесты**

- Unit test: mock HTTP server возвращает redirect на HTTP

---

### BP-013 — Logger swallowed exception

**Приоритет:** Medium
**Категория:** Reliability
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`Logger.RotateLogFile` (строка 73-77) содержит `catch` без обработки, который тихо перезаписывает файл пустой строкой при ошибке ротации.

**Подтверждение**
- `AiteBar/Logger.cs:73-77`

**Почему это важно**

Пользователь может потерять все логи без какого-либо уведомления.

**Рекомендация**

Логировать ошибку ротации через `Debug.WriteLine` или `Trace.TraceError`.

**Критерии готовности**

- При ошибке ротации логируется хотя бы сообщение в Debug output

**Необходимые тесты**

- Unit test: mock файловой системы, симуляция ошибки File.Move

---

### BP-014 — _saveSemaphore не защищает чтение

**Приоритет:** Medium
**Категория:** Concurrency
**Уверенность:** Confirmed
**Стоимость:** S
**Риск изменения:** Medium

**Проблема**

`_saveSemaphore` в `AppSettingsService` защищает `SaveAsync`, но `Settings` property и `Elements` property читаются без блокировки. `NormalizeAppState()` модифицирует `_appSettings` без блокировки.

**Подтверждение**
- `AiteBar/AppSettingsService.cs:16` — `_saveSemaphore`
- `AiteBar/AppSettingsService.cs:31` — `Settings` property (без lock)
- `AiteBar/AppSettingsService.cs:279-363` — `NormalizeAppState` (без lock)

**Почему это важно**

Concurrent чтение и запись может привести к corrupt state.

**Рекомендация**

Добавить `lock(_stateLock)` в `NormalizeAppState()` и в `Settings` getter.

**Критерии готовности**

- Нет race condition при параллельных операциях

**Необходимые тесты**

- Concurrent access test

---

### BP-015 — MaxSettingsFileBytes = 100MB

**Приоритет:** Medium
**Категория:** Configuration
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

100MB limit для settings файла. При corrupted JSON десериализация может потреблять много памяти и время.

**Подтверждение**
- `AiteBar/AppSettingsService.cs:18`

**Почему это важно**

Нормальный settings файл — ~10-50KB. 100MB — чрезмерно.

**Рекомендация**

Снизить до 10MB.

**Критерии готовности**

- Settings файл > 10MB отклоняется

**Необходимые тесты**

- Unit test: `EnsureFileSizeWithinLimit` с 11MB файлом

---

### BP-016 — SettingsWindow.LoadProfilesAsync fire-and-forget

**Приоритет:** Medium
**Категория:** Architecture
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

```csharp
_ = LoadProfilesAsync().ContinueWith(
    t => Logger.Log(t.Exception!.GetBaseException()),
    TaskContinuationOptions.OnlyOnFaulted);
```

`t.Exception!` — оператор `!` опасен: если task завершился успешно, `t.Exception` будет `null`, и `!` не поможет (это nullable-forgiving, не null-check). Внутри `OnlyOnFaulted` это безопасно, но код выглядит обманчиво.

**Подтверждение**
- `AiteBar/SettingsWindow.xaml.cs:56-58`

**Почему это важно**

Может ввести в заблуждение при рефакторинге.

**Рекомендация**

Убрать `!`: `t.Exception?.GetBaseException()` или `t.Exception!.GetBaseException()` (уже безопасно внутри OnlyOnFaulted).

**Критерии готовности**

- Код не содержит nullable warnings

**Необходимые тесты**

- Compilation check

---

### BP-017 — BuildDuplicateElementName потенциально бесконечный цикл

**Приоритет:** Medium
**Категория:** Reliability
**Уверенность:** Probable
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

```csharp
for (int index = 2; ; index++)
```

Нет верхнего предела. Если `Elements` содержит все возможные имена `Name (копия N)`, цикл не завершится.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:445-449`

**Почему это важно**

На практике маловероятно, но нет guard от infinite loop.

**Рекомендация**

Добавить `index < 10000` как guard.

**Критерии готовности**

- Цикл завершается при любом количестве элементов

**Необходимые тесты**

- Unit test: 1000 элементов с дублирующимися именами

---

### BP-018 — Brush conversion без кэширования

**Приоритет:** Medium
**Категория:** Performance
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`_brushConverter.ConvertFromString(item.Color)` вызывается в `CreateUnifiedButton` для каждой кнопки при каждом `RefreshPanel()`.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:1458`

**Почему это важно**

`BrushConverter.ConvertFromString` аллоцирует новый `SolidColorBrush` при каждом вызове. Для 20+ кнопок это 20+ аллокаций при каждом обновлении.

**Рекомендация**

Кэшировать brush объекты по hex-строке.

**Критерии готовности**

- Кэш работает, brush создаётся один раз для каждого цвета

**Необходимые тесты**

- Performance test: 100 RefreshPanel без кэша vs с кэшем

---

### BP-019 — BrowserHelper swallowed exceptions

**Приоритет:** Medium
**Категория:** Security
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`GetPathFromRegistry` (строка 79) содержит `catch { return null; }` — полностью подавляет все исключения без логирования.

**Подтверждение**
- `AiteBar/BrowserHelper.cs:79`

**Почему это важно**

Приblems с реестром (permission denied, corrupt data) будут невидимы.

**Рекомендация**

Логировать через `Debug.WriteLine`.

**Критерии готовности**

- Ошибки реестра логируются в Debug output

**Необходимые тесты**

- Unit test: mock registry failure

---

### BP-020 — Неиспользуемые поля _currentUnifiedButtons и _activeContextElements

**Приоритет:** Medium
**Категория:** Architecture
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`_currentUnifiedButtons` (строка 105) и `_activeContextElements` (строка 106) объявлены, но `_activeContextElements` нигде не читается.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:105-106`

**Почему это важно**

Мёртвый код увеличивает cognitive load.

**Рекомендация**

Удалить `_activeContextElements`.

**Критерии готовности**

- Компиляция без ошибок

**Необходимые тесты**

- Build check

---

### BP-021 — architecture.md: MaxUserBands = 2 vs фактически 3

**Приоритет:** Medium
**Категория:** Documentation
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`architecture.md:555` утверждает `MaxUserBands = 2`, но `PanelLayoutHelper.cs:12` содержит `MaxUserBands = 3`.

**Подтверждение**
- `docs/architecture.md:555`
- `AiteBar/PanelLayoutHelper.cs:12`

**Рекомендация**

Исправить `architecture.md` на `MaxUserBands = 3`.

---

### BP-022 — architecture.md: _quickNoteWindow в ActionService

**Приоритет:** Medium
**Категория:** Documentation
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`architecture.md:321-323` описывает `_quickNoteWindow`, `_timerStopwatchWindow`, `_fileSorterWindow` как компоненты `ActionService`. Фактически эти окна создаются через `UtilityRegistry` и `UtilityBase<TWindow>`, а `ActionService` не содержит этих полей.

**Подтверждение**
- `docs/architecture.md:321-323`
- `AiteBar/ActionService.cs` — нет таких полей

**Рекомендация**

Обновить `architecture.md` согласно фактической архитектуре.

---

### BP-023 — TelemetryService.Flush может не дождаться

**Приоритет:** Medium
**Категория:** Reliability
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

```csharp
_ = SentrySdk.FlushAsync(timeout).ContinueWith(task => { ... }, OnlyOnFaulted);
```

`FlushAsync` возвращает Task, но результат не awaited. При завершении приложения Sentry может не отправить данные.

**Подтверждение**
- `AiteBar/TelemetryService.cs:175-196`

**Рекомендация**

В `App.OnExit` делать `await TelemetryService.FlushAsync(timeout)`.

---

### BP-024 — ZipFile.ExtractToDirectory без path traversal check

**Приоритет:** Medium
**Категория:** Security
**Уверенность:** Probable
**Стоимость:** S
**Риск изменения:** Low

**Проблема**

`ZipFile.ExtractToDirectory(packagePath, tempRoot)` может извлечь файлы с path traversal (`../../../etc/passwd`). В Windows это менее критично, но формально это vulnerability.

**Подтверждение**
- `AiteBar/PanelPackageService.cs:152`

**Почему это важно**

ZIP Slip — известная уязвимость. Хотя `tempRoot` — временная директория, проверка не помешает.

**Рекомендация**

После извлечения проверять что все файлы находятся внутри `tempRoot`.

**Критерии готовности**

- Все извлечённые файлы внутри tempRoot

**Необходимые тесты**

- Unit test: ZIP с path traversal entry

---

### BP-025 — Window_Loaded fire-and-forget startup

**Приоритет:** Medium
**Категория:** Architecture
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

```csharp
_ = CompleteDeferredStartupAsync().ContinueWith(
    task => _ = Logger.LogAsync(task.Exception!.GetBaseException()),
    TaskContinuationOptions.OnlyOnFaulted);
```

Если startup упадёт, UI остаётся в частично-инициализированном состоянии.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:1239-1241`

**Рекомендация**

Показать пользователя dialog с ошибкой при failure startup.

---

### BP-026 — _startupCts.Dispose в finally

**Приоритет:** Medium
**Категория:** Concurrency
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`_startupCts.Dispose()` вызывается в `finally` блоке `CompleteDeferredStartupAsync`. Если `Dispose()` вызовется до завершения `Task.Run`, token будет disposed во время использования.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:1313-1343`

**Рекомендация**

Вызывать `Dispose()` после завершения task, а не в finally.

---

### BP-027 — GetTargetScreen fallback на null

**Приоритет:** Medium
**Категория:** Reliability
**Уверенность:** Probable
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`Screen.PrimaryScreen` может вернуть null на headless/RDP disconnect.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:664-670`

**Рекомендация**

Добавить null check и fallback на `SystemParameters`.

---

### BP-028 — SettingsWindow 853 строки

**Приоритет:** Low
**Категория:** Code Quality
**Уверенность:** Confirmed
**Стоимость:** M
**Риск изменения:** Medium

**Проблема**

`SettingsWindow.xaml.cs` — 853 строки, смешивает UI логику и бизнес-логику.

**Подтверждение**
- `AiteBar/SettingsWindow.xaml.cs`

**Рекомендация**

Вынести profile loading и validation в отдельный service-class.

---

### BP-029 — QuickNoteWindow 1019 строк

**Приоритет:** Low
**Категория:** Code Quality
**Уверенность:** Confirmed
**Стоимость:** M
**Риск изменения:** Medium

**Проблема**

`QuickNoteWindow.xaml.cs` — 1019 строк, complex state management с 10+ приватными полями.

**Подтверждение**
- `AiteBar/QuickNoteWindow.xaml.cs`

**Рекомендация**

Вынести save/debounce логику в `QuickNoteService`.

---

### BP-030 — MainWindow 30+ приватных полей

**Приоритет:** Low
**Категория:** Architecture
**Уверенность:** Confirmed
**Стоимость:** S
**Риск изменения:** Low

**Проблема**

30+ приватных полей для UI state затрудняют понимание.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:50-120`

**Рекомендация**

Сгруппировать related fields в private record/class.

---

### BP-031 — DispatcherTimer 30ms

**Приоритет:** Low
**Категория:** Performance
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`_timer` опрашивает позицию курсора каждые 30ms. Это ~33 вызова в секунду.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:50`

**Почему это важно**

Для edge-панели это оправдано (нужна быстрая реакция). Но можно увеличить до 50ms без заметной потери отзывчивости.

---

### BP-032 — ExecuteCommand передаёт строку в cmd.exe

**Приоритет:** Low
**Категория:** Security
**Уверенность:** Confirmed
**Стоимость:** S
**Риск изменения:** Medium

**Проблема**

`ActionService.ExecuteCommand` передаёт пользовательскую строку в `cmd.exe /c {command}`. Это by design — команда выполняется через shell. Но нет documentation о том что quoting responsibility на пользователе.

**Подтверждение**
- `AiteBar/ActionService.cs:263-274`

**Рекомендация**

Документировать в technical-reference.md что команда передаётся как-is.

---

### BP-033 — Logger теряет структуру стека

**Приоритет:** Low
**Категория:** Reliability
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

```csharp
string safeExceptionText = ex.ToString()
    .Replace("\r\n", "\n")
    .Replace('\r', '\n')
    .Replace("\n", " | ");
```

Замена `\n` на ` | ` превращает multi-line stack trace в одну строку, теряя читаемость.

**Подтверждение**
- `AiteBar/Logger.cs:49-52`

**Рекомендация**

Использовать `Environment.NewLine` или оставлять `\n` как есть.

---

### BP-034 — CreateMenuItem создаёт TextBlock на каждый вызов

**Приоритет:** Low
**Категория:** Performance
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`CreateMenuItem` создаёт новый `TextBlock` для glyph иконки при каждом вызове. Нет пулинга/кэширования.

**Подтверждение**
- `AiteBar/MainWindow.xaml.cs:225-260`

**Рекомендация**

Кэшировать TextBlock по glyph+color.

---

### BP-035 — USER_MANUAL.md TODO скриншоты

**Приоритет:** Low
**Категория:** Documentation
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`USER_MANUAL.md` содержит 3 TODO для вставки скриншотов.

**Подтверждение**
- `docs/USER_MANUAL.md:51`, `66`, `125`

**Рекомендация**

Подготовить скриншоты перед релизом.

---

### BP-036 — AllowUnsafeBlocks=true

**Приоритет:** Low
**Категория:** Build
**Уверенность:** Probable
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`AiteBar.csproj` содержит `AllowUnsafeBlocks=true`, но unsafe code не обнаружен в исходниках.

**Подтверждение**
- `AiteBar/AiteBar.csproj:11`

**Рекомендация**

Проверить используется ли. Если нет — удалить.

---

### BP-037 — GenerateAssemblyInfo=false

**Приоритет:** Low
**Категория:** Build
**Уверенность:** Confirmed
**Стоимость:** XS
**Риск изменения:** Low

**Проблема**

`GenerateAssemblyInfo=false` отключает автоматическую генерацию AssemblyInfo. Версия задаётся вручную.

**Подтверждение**
- `AiteBar/AiteBar.csproj:13`

**Почему это информации**

Это valid approach для projects с ручным управлением версиями.

---

### BP-038 — Нет DI-контейнера

**Приоритет:** Info
**Категория:** Architecture
**Уверенность:** Confirmed
**Стоимость:** —
**Риск изменения:** —

**Проблема**

Нет DI-контейнера (Microsoft.Extensions.DependencyInjection). Сервисы создаются вручную.

**Почему это информации**

Для desktop-приложения такого масштаба (~60 файлов) DI-контейнер избыточен. Текущий подход с constructor injection через интерфейсы достаточен.

---

### BP-039 — Code-behind вместо MVVM

**Приоритет:** Info
**Категория:** Architecture
**Уверенность:** Confirmed
**Стоимость:** —
**Риск изменения:** —

**Проблема**

Приложение использует code-behind подход вместо MVVM.

**Почему это информации**

Для WPF desktop-приложения с прямой системной интеграцией (Win32 API, mouse hooks, tray) code-behind оправдан. MVVM добавил бы сложность без пропорциональной пользы.

---

### BP-040 — Directory.Build.props

**Приоритет:** Info
**Категория:** Build
**Уверенность:** Confirmed
**Стоимость:** —
**Риск изменения:** —

**Проблема**

`Directory.Build.props` содержит `Deterministic=true` и `RestorePackagesWithLockFile=true`. Это good practices.

**Подтверждение**
- `Directory.Build.props`

---

### BP-041 — Sentry отключен по умолчанию

**Приоритет:** Info
**Категория:** Security
**Уверенность:** Confirmed
**Стоимость:** —
**Риск изменения:** —

**Проблема**

Sentry отключен по умолчанию. Включение возможно только через env vars или settings.json.

**Подтверждение**
- `AiteBar/TelemetryService.cs:37-51`

### BP-042 — dotnet format ~250+ ошибок whitespace и end-of-line

**Приоритет:** Medium
**Категория:** Code Quality
**Уверенность:** Confirmed
**Стоимость:** S
**Риск изменения:** Low

**Проблема**

`dotnet format --verify-no-changes` обнаружил ~250+ ошибок форматирования в 9 файлах:

- **Whitespace** (неправильные отступы): `ActionService.cs` (12), `App.xaml.cs` (3), `AppSettingsService.cs` (~100 — смешанные отступы в `NormalizeAppState`, `NormalizeElements`, `AreElementsEquivalent`), `IconConverterService.cs` (1), `Logger.cs` (~15 — смешанные отступы в `RotateLogFile`)
- **End-of-line** (LF вместо CRLF): `RuntimeLocalizationInfrastructureTests.cs`, `RuntimeLocalizationWindowSourceTests.cs`, `UpdateCheckServiceTests.cs`, `WpfTestCollection.cs`

**Подтверждение**
- `dotnet format --verify-no-changes` — 250+ ошибок в 9 файлах

**Почему это важно**

Неконсистентное форматирование затрудняет code review (diff включает whitespace-only изменения) и может привести к merge conflicts.

**Рекомендация**

Выполнить `dotnet format .\AiteBar.sln` для автоматического исправления. Добавить `dotnet format --verify-no-changes` в CI workflow.

**Критерии готовности**

- `dotnet format --verify-no-changes` возвращает 0 ошибок

**Необходимые тесты**

- CI check: format verification на каждом PR

---

## 6. Архитектура и разделение ответственности

### MainWindow.xaml.cs — детальный разбор

**Реальный размер:** 2776 строк, ~30 приватных полей, ~50 методов

**Ответственность:**
1. Lifecycle панели (show/hide/toggle/animate)
2. Layout computation delegation
3. Button creation и rendering
4. Drag-and-drop reorder
5. Drag-handle side switching
6. Context switching
7. Keyboard navigation
8. Tray icon management
9. Mouse hook integration (delegates to NativeIntegrationService)
10. Context menu building
11. Import/Export delegation
12. DPI awareness
13. Image loading/caching
14. Window positioning
15. Hotkey registration delegation

**Самые сложные участки:**
- `RefreshPanel()` — 35 строк, координирует всё обновление UI
- Drag-and-drop reorder — 100+ строк inline в `CreateUnifiedButton`
- `Toggle()` — 30 строк, сложная анимация с 2 completed handlers

**Безопасные границы декомпозиции:**
1. Keyboard navigation → отдельный partial class или helper
2. Tray icon → отдельный helper
3. Drag-and-drop reorder → отдельный helper

**Не нужно выносить:**
- `ShowDock/HideDock/ToggleDock` — тесно связаны с lifecycle
- `RefreshPanel` — центральная точка
- `UpdateOrientation` — связан с XAML элементами

**Порядок рефакторинга:**
1. BP-006: Убрать double event handling (XS)
2. BP-009: Объединить context switching methods (S)
3. BP-003: Вынести keyboard navigation (S)
4. BP-003: Вынести tray icon (XS)
5. BP-003: Вынести drag-and-drop reorder (M)

---

## 7. Безопасность

### Таблица угроз

| Объект | Угроза | Текущая защита | Недостаток | Рекомендация | Приоритет |
|--------|--------|----------------|------------|--------------|-----------|
| ExecuteCommand | Command injection через cmd.exe | Confirmation dialog + dangerous syntax check | Нет quoting, команда передаётся как-is | Документировать поведение | Low |
| ExecuteScriptFile | Запуск вредоносного скрипта | Confirmation dialog | Нет sandboxing | OK — пользователь подтверждает | Info |
| ZipFile.ExtractToDirectory | ZIP Slip path traversal | Temporary directory | Нет проверки после извлечения | Проверять что файлы внутри tempRoot | Medium |
| IconHelper.DownloadFaviconAsync | HTTP downgrade | HTTPS URL | Нет проверки redirect | Проверять final URL scheme | High |
| BrowserHelper.GetPathFromRegistry | Registry access failure | try-catch | Swallowed exceptions | Логировать ошибки | Medium |
| OpenUrl | Opening malicious URLs | URI scheme validation (http/https only) | OK | — | Info |
| UpdateCheckService | SSRF | Trusted GitHub URL validation | OK | — | Info |
| Settings file | Corrupt JSON | File size limit, backup/restore | 100MB limit too high | Reduce to 10MB | Medium |
| TelemetryService | PII leakage | SendDefaultPii = false by default | OK | — | Info |
| Hotkey injection | Sending hotkeys to wrong window | SendInput to foreground window | OK — user initiates | — | Info |

---

## 8. Надёжность и данные

### Настройки

- **Атомарность записи:** Тemp file + File.Replace — good
- **Backup:** 5-level rotation — good
- **Restore:** TryLoadFromBackup с 5 попыток — good
- **Размер limit:** 100MB — слишком много (BP-015)
- **Миграция:** custom_buttons.json → settings.json — работает

### Quick Note

- **Конфликтные копии:** QuickNote.conflict-{timestamp}.md — good
- **Внешние изменения:** HasExternalChanges через WriteTimeUtc — good
- **Сохранение:** DispatcherTimer debounce 700ms — good

### Импорт

- **Ограничения размера:** 25MB file, 2MB manifest, 10MB entry, 50MB uncompressed, 256 entries — good
- **Валидация:** PanelPackageMapper.IsPackagedImagePathSafe — good
- **Path traversal:** Нет проверки (BP-024)

---

## 9. Производительность

### Подтверждённые проблемы

| Проблема | Влияние | Рекомендация |
|----------|---------|--------------|
| `_buttonImageCache` — brush без кэширования (BP-018) | Low | Кэшировать brush по hex |
| `_timer` 30ms polling (BP-031) | Low | Оправдано для edge-面板 |
| `CreateMenuItem` — TextBlock на каждый вызов (BP-034) | Low | Кэшировать |

### Потенциальные риски

| Риск | Вероятность | Что измерить |
|------|-------------|-------------|
| `RefreshPanel()` при большом количестве кнопок | Medium | Timing на 50+ кнопках |
| `BrowserHelper.GetProfiles()` — чтение JSON профилей | Low | Timing на 10+ профилях |
| `IconHelper.DownloadFaviconAsync` — network latency | Low | Timeout на медленном интернете |

### Точки для измерений

1. `RefreshPanel()` — добавить `Stopwatch` для timing
2. `LoadUnifiedButtonImageAsync` — timing загрузки изображений
3. `BrowserHelper.GetProfiles` — timing чтения файлов

---

## 10. WPF, UI и доступность

### XAML и стили

- Хорошее использование ресурсов (`FormControlsResources.xaml`, `SettingsResources.xaml`)
- Consistent design system описан в `DESIGN.md`
- Корректное использование `CornerRadius`, `DropShadowEffect`
- `AllowsTransparency=True` для panel windows — оправдано

### DPI

- `_cachedDpi` correctly obtained from `PresentationSource`
- Screen coordinates divided by DPI for WPF positioning
- `Math.Round` для sub-pixel values (prevents flicker)

### Keyboard Navigation

- Tab/arrow key navigation implemented
- Focus visual styles for keyboard mode
- `Focusable = true` на кнопках

### Accessibility

- Нет `AutomationProperties` на элементах
- Нет `ToolTip` на некоторых элементах
- Нет high contrast theme support

### Локализация

- Runtime culture switching — good
- `LocExtension` для XAML bindings — good
- Fallback на English — good

### Анимации

- Централизованы в `Constants.cs` — good
- `EasingHelper` для consistent easing — good
- `Storyboard` для context transitions — good

---

## 11. Тестирование

### Матрица покрытия

| Компонент | Текущее покрытие сценариев | Недостающие сценарии | Рекомендуемый тип теста | Приоритет |
|-----------|--------------------------|---------------------|-----------------------|-----------|
| ActionService | Хорошее (dangerous commands, web action, hotkey) | Concurrent hotkey injection, edge cases | Unit | High |
| AppSettingsService | Хорошее (backup, normalize, save/load) | Concurrent access, corrupt JSON recovery | Unit + Integration | High |
| PanelLayoutHelper | Отличное (horizontal, vertical, bands) | Edge cases (0 buttons, 100 buttons) | Unit | Medium |
| HotkeyService | Хорошее (registration, conflicts) | Win key modifier, reserved hotkeys | Unit | Medium |
| PanelPackageService | Хорошее (export, import, validation) | ZIP Slip, corrupt manifest | Unit + Security | High |
| QuickNoteService | Хорошее (load, save, conflict) | External changes, large files | Unit | Medium |
| LocalizationService | Хорошее (culture, format, normalize) | Missing resource keys | Unit | Low |
| MainWindow | Нет (UI) | All UI scenarios | Manual | High |
| SettingsWindow | Нет (UI) | All settings scenarios | Manual | Medium |
| NativeIntegrationService | Нет (native) | Hook install/uninstall | Manual | Medium |
| TelemetryService | Нет (external) | Sentry init, flush | Unit with mock | Low |
| UpdateCheckService | Хорошее (version parsing, URL validation) | Network timeout, invalid JSON | Unit | Medium |

### Качество тестов

- Хорошее использование `[Theory]` с `[InlineData]`
- Fake runtime для `ActionService` (`FakeActionServiceRuntime`)
- Fake registrar для `HotkeyService` (`FakeHotkeyRegistrar`)
- Нет mocking framework (чистый fake objects) — OK для этого масштаба

---

## 12. Сборка, зависимости и CI/CD

### Результаты команд

| Команда | Результат | Причина |
|---------|-----------|---------|
| `dotnet restore` | Error | DLL locked by external process |
| `dotnet build -c Release` | Error | `AiteBar.dll` in obj locked |
| `dotnet test -c Release` | Error | Build dependency failed |
| `dotnet format --verify-no-changes` | Not executed | Requires successful build |
| `dotnet list package --outdated` | Not executed | Requires successful build |
| `dotnet list package --vulnerable` | Not executed | Requires successful build |

### Зависимости

| Пакет | Версия | Назначение |
|-------|--------|-----------|
| Sentry | 6.6.0 | Telemetry |
| SkiaSharp | 3.119.2 | Icon conversion |
| SkiaSharp.NativeAssets.Win32 | 3.119.2 | SkiaSharp native |
| Svg.Skia | 5.1.0 | SVG rendering |

### CI/CD

- GitHub Actions workflows **отсутствуют**
- Dependabot configuration **отсутствует**
- CodeQL **не настроен**

### Release Process

- `Build-Installer.ps1` — Inno Setup
- Version sync: `AiteBar.csproj` → `AiteBar.iss` → installer
- Code signing через signtool (optional)
- SHA256 checksum не генерируется

---

## 13. Расхождения документации и реализации

| ID | Документ | Заявлено | Реализовано | Требуемое действие |
|----|----------|----------|-------------|-------------------|
| DOC-001 | architecture.md | `MaxUserBands = 2` | `MaxUserBands = 3` | Исправить на `3` |
| DOC-002 | architecture.md | `_quickNoteWindow` в `ActionService` | Нет такого поля | Убрать из описания |
| DOC-003 | architecture.md | `_timerStopwatchWindow` в `ActionService` | Нет такого поля | Убрать из описания |
| DOC-004 | architecture.md | `_fileSorterWindow` в `ActionService` | Нет такого поля | Убрать из описания |
| DOC-005 | AGENTS.md | `.github/workflows/build-test.yml` | Workflow не существует | Создать или удалить описание |
| DOC-006 | AGENTS.md | `.github/workflows/codeql.yml` | Workflow не существует | Создать или удалить описание |
| DOC-007 | AGENTS.md | `.github/dependabot.yml` | Конфиг не существует | Создать или удалить описание |
| DOC-008 | USER_MANUAL.md | TODO скриншоты | 3 TODO | Подготовить скриншоты |
| DOC-009 | technical-reference.md | Opera, OperaGX, Vivaldi в UI | Только Chrome, Edge, Brave, Yandex, Firefox | OK — документировано |

---

## 14. План улучшений

### Этап 1 — критические исправления

| Задача | Зависимости | Ожидаемый эффект | Риски | Критерии завершения |
|--------|-------------|------------------|-------|---------------------|
| BP-001: `Handled = true` | Нет | Приложение не падает при unhandled exception | Может скрыть серьёзные баги | Crash recovery работает |
| BP-004: Finalizer для NativeIntegrationService | Нет | Hook снимается при crash | Min | Finalizer срабатывает |
| BP-002: Lock для _appSettings | Нет | Нет data race | Possible deadlock | Concurrent test passes |

### Этап 2 — снижение технического долга

| Задача | Зависимости | Ожидаемый эффект | Риски | Критерии завершения |
|--------|-------------|------------------|-------|---------------------|
| BP-005: GitHub Actions | Этап 1 | CI/CD, automated tests | Min | Workflow runs on push |
| BP-003: MainWindow decomposition | Этап 1 | Читаемость, тестируемость | Medium — UI regression | Все manual tests pass |
| BP-007: SettingsWindow interface | Этап 1 | Тестируемость | Medium | Unit test passes |
| BP-009: Unify context switching | Этап 1 | Меньше дублирования | Low | Все 4 способа работают |

### Этап 3 — качество и сопровождение

| Задача | Зависимости | Ожидаемый эффект | Риски | Критерии завершения |
|--------|-------------|------------------|-------|---------------------|
| BP-015: Снизить MaxSettingsFileBytes | Нет | Меньше памяти | Low | 10MB файл отклоняется |
| BP-018: Кэшировать brush | Нет | Меньше аллокаций | Min | Performance test |
| DOC-001-007: Исправить документацию | Нет | Точность docs | Min | Документация соответствует коду |
| BP-028-029: Декомпозиция SettingsWindow/QuickNoteWindow | Этап 2 | Читаемость | Medium | Все тесты проходят |

---

## 15. Быстрые улучшения

| ID | Изменение | Стоимость | Эффект |
|----|-----------|-----------|--------|
| BP-001 | `args.Handled = true` | XS | Приложение не падает |
| BP-004 | Добавить finalizer | XS | Hook снимается |
| BP-008 | Заменить `Thread.Sleep` на `Task.Delay` | XS | Нет UI freeze |
| BP-013 | Логировать ошибку ротации | XS | Видимость ошибок |
| BP-015 | Снизить MaxSettingsFileBytes до 10MB | XS | Меньше памяти |
| BP-016 | Убрать `!` из ContinueWith | XS | Читаемость |
| BP-017 | Добавить guard в for loop | XS | Нет infinite loop |
| BP-019 | Логировать registry errors | XS | Debug visibility |
| BP-020 | Удалить `_activeContextElements` | XS | Меньше мёртвого кода |
| DOC-001-007 | Исправить документацию | XS | Точность docs |
| BP-042 | Запустить `dotnet format` | S | Чистый формат |
| UPD-001 | Обновить SkiaSharp до 3.119.4 | XS | Актуальные зависимости |

---

## 16. Что не рекомендуется менять

1. **Code-behind подход** — оправдан для WPF desktop-приложения с прямой Win32 интеграцией
2. **PanelLayoutHelper** — чистая математика, хорошо протестирована, не требует изменений
3. **AppSettingsService backup/restore** — надёжная система, не требует изменений
4. **HotkeyService** — чистый интерфейс с mock-тестами, не требует изменений
5. **LocalizationService** — работающая система с runtime switching, не требует изменений
6. **Constants.cs** — централизованные константы, good practice
7. **UtilityRegistry** — расширяемая система утилит, не требует изменений
8. **Mutex для single instance** — работает корректно
9. **Inno Setup installer** — adequately configured
10. **Directory.Build.props** — deterministic builds, lock files

---

## 17. Итоговый приоритетный backlog

| Порядок | ID | Задача | Приоритет | Стоимость | Зависимости | Ожидаемый результат |
| ------: | -- | ------ | --------- | --------- | ----------- | ------------------- |
| 1 | BP-001 | `Handled = true` | Critical | XS | — | Приложение не падает |
| 2 | BP-004 | Finalizer для NativeIntegrationService | Critical | XS | — | Hook снимается |
| 3 | BP-002 | Lock для _appSettings | Critical | M | — | Нет data race |
| 4 | BP-005 | GitHub Actions | High | M | — | CI/CD работает |
| 5 | BP-008 | Заменить Thread.Sleep | High | XS | — | Нет UI freeze |
| 6 | BP-006 | Убрать double DragHandle handling | High | S | — | Save once |
| 7 | BP-009 | Unify context switching | High | S | — | Меньше дублирования |
| 8 | BP-010 | Error reporting для fire-and-forget save | High | S | — | Видимость ошибок |
| 9 | BP-015 | Снизить MaxSettingsFileBytes | Medium | XS | — | Меньше памяти |
| 10 | BP-013 | Логировать ошибку ротации | Medium | XS | — | Debug visibility |
| 11 | BP-018 | Кэшировать brush | Medium | XS | — | Меньше аллокаций |
| 12 | DOC-001 | Исправить MaxUserBands в docs | Medium | XS | — | Точность docs |
| 13 | DOC-005-007 | Создать GitHub workflows | Medium | M | BP-005 | CI/CD |
| 14 | BP-003 | MainWindow decomposition | High | L | Этап 2 | Читаемость |
| 15 | BP-007 | SettingsWindow interface | High | M | Этап 2 | Тестируемость |
| 16 | BP-042 | `dotnet format` — исправить ~250 ошибок | Medium | S | — | Чистый формат |
