# Code Review перед релизом v1.11.0

**Ветка:** `master` | **Диапазон:** `9668b50..HEAD` | **Файлов:** 65 | **Строк:** +6349 / -1128

**Сборка:** Release — ✅ 0 ошибок, 0 предупреждений  
**Тесты:** ✅ Все 507 тестов пройдены (fallback-метод `dotnet vstest`)

---

## CRITICAL (3)

| # | Область | Файл:Строка | Проблема | Статус |
|---|---------|-------------|----------|--------|
| C1 | Clipboard | `ClipboardHistoryService.cs:148` | **NRE: `text.Length` при null** — `Clipboard.GetText()` может вернуть `null` даже после `ContainsText()` (race condition с другим процессом). Вызов `text.Length` бросает `NullReferenceException`. | ✅ Исправлено |
| C2 | Settings | `AppSettingsService.cs:64` | **GC pressure: `CloneAppSettings` через JSON serialize/deserialize на каждый доступ к `Settings`** — горячий путь в UI bind-loop создаёт непрерывное давление на сборщик мусора. | ✅ Исправлено (ручной глубокий клон) |
| C3 | Logger | `Logger.cs:50-89` | **Recursive stack overflow risk** — `FlushQueue` делает double-check с рекурсивным вызовом `FlushQueue()`. Под высокой нагрузкой логирования возможен stack overflow. | ✅ Исправлено (заменено циклом) |

---

## MAJOR (10)

| # | Область | Файл:Строка | Проблема | Статус |
|---|---------|-------------|----------|--------|
| M1 | Clipboard | `ClipboardHistoryService.cs:185-191` | **Дедупликация изображений по длине байтов** — два разных изображения одной длины будут считаться дубликатами и молча отброшены. Нужен хеш или `SequenceEqual`. | ✅ Исправлено |
| M2 | Clipboard | `ClipboardHistoryService.cs:20` | **Нет лимита на размер изображений** — `MaxTextLength` ограничен 10KB, но изображения без ограничения. 50-мегабайтный скриншот × 50 записей = 2.5GB в памяти. | ✅ Исправлено |
| M3 | Clipboard | `ClipboardHistoryService.cs:18-19` | **История clipboard хранит пароли/токены без очистки при выходе** — `_entries` живёт весь процесс. По требованиям privacy в AGENTS.md нужна очистка при dispose/shutdown. | ✅ Исправлено |
| M4 | Settings | `AppSettingsService.cs:237-239` | **Race condition в `SaveAsync`** — мутация `_appSettings.Elements` внутри lock, но сериализация snapshotนอก lock. Параллельный `SaveAsync` может повредить данные. | ✅ Исправлено (снимок создается внутри lock) |
| M5 | Processes | `ActionService.cs:72,267-275,346-358` | **Утечка process handles** — `StartProcess` возвращает `IActionProcessHandle?`, который ни разу не disposed во всём `ActionService`. | ✅ Исправлено |
| M6 | Thread safety | `BrowserHelper.cs:26` | **`Dictionary<>` без синхронизации** — `_userDataPathOverrides` доступен из public static методов; параллельный доступ из тестов или production может повредить dictionary. | ✅ Исправлено (ConcurrentDictionary) |
| M7 | Thread safety | `PathHelper.cs:13` | **Data race на `_appDataFolderOverride`** — static mutable поле без синхронизации, используется из параллельных тестов. | ✅ Исправлено (блокировка) |
| M8 | QR Code | `QRCodeGeneratorWindow.xaml.cs:84-128` | **Синхронная генерация QR на UI thread** — `RefreshPreviewAsync` вызывает `GenerateQrData` и `XamlQRCode.GetGraphic` синхронно до первого await. Замораживает UI при больших входных данных. | ✅ Исправлено (генерация в Task.Run с Freeze для UI безопасности) |
| M9 | DnD | `MainWindow.DragAndDropHandler.cs:79` | **`_draggedOriginalIndex` может быть -1** — если кнопка не найдена в `_unifiedButtons` (обновление панели во время drag), `_currentUnifiedButtons[_draggedOriginalIndex]` бросает `ArgumentOutOfRangeException`. | ✅ Исправлено |
| M10 | DnD | `MainWindow.DragAndDropHandler.cs:82` | **`RefreshPanel` не проверяет drag state** — если hotkey/settings-change вызовет `RefreshPanel` во время drag-and-drop, `_draggedOriginalIndex` устареет и reorder сломается. | ✅ Исправлено |

---

## MINOR (18)

| # | Область | Проблема | Статус |
|---|---------|----------|--------|
| m1 | Clipboard | `ToLower()` вместо `ToLowerInvariant()` в поиске — проблемы с турецким `İ` | ✅ Исправлено |
| m2 | Clipboard | UI пересоздаётся с нуля при каждом clipboard change (50 элементов × 5 визуальных контролов) | ❌ Не исправлено |
| m3 | Clipboard | `_suppressNextChange` — plain `bool` без `volatile` | ❌ Не исправлено |
| m4 | Clipboard | `DisplayText.Substring(0, 50)` может разрезать surrogate pair | ✅ Исправлено |
| m5 | QR Code | `Margin=4` хардкод, хотя сервис поддерживает margin параметр |
| m6 | QR Code | Bounds mismatch: сервис пускает pixelSize 1..100, UI слайдер 4..32 |
| m7 | QR Code | `ParseColorBytes` доверяет caller pre-normalize input |
| m8 | Panel | `OverflowWrapPanel` measure/arrange используют разные `availableSize` vs `finalSize` для подсчёта колонок |
| m9 | Panel | `GetArrangedRectForIndex` дублирует логику ArrangeVertical/ArrangeHorizontal — может рассинхронизироваться |
| m10 | Panel | `TaskbarPositionIndicatorService` обновляет z-order каждые 250ms даже без изменений |
| m11 | Panel | `RefreshPanel` вызывает `UpdateOrientation` дважды (лишняя работа) |
| m12 | Panel | Redundant ternaries: `isVertical ? Left : Left` в MainWindow |
| m13 | Settings | `TryLoadFromBackup` — sync `File.ReadAllText` при наличии async версии |
| m14 | Settings | `NormalizeElements` создаёт полную копию элемента при `null` rotation list даже если ничего не изменилось |
| m15 | Hotkey | `RegisterAll` не откатывает уже зарегистрированные hotkeys при ошибке регистрации |
| m16 | UI | `BorderColor` (`#2A2A2A`) используется как `Background` — семантически неправильно |
| m17 | UI | `PanelBackground` потерял alpha channel (`#F01F1F1F` → `#1F1F1F`) |
| m18 | Tests | Параллельные тесты мутируют глобальные env vars — флейки при parallel execution |

---

## NIT (10)

| # | Проблема |
|---|----------|
| n1 | `Debug.WriteLine` в `TaskbarPositionIndicatorService` — шум в debug sessions |
| n2 | Дублирующиеся цвета иконок: `FileSorter`/`QRCodeGenerator` = `#60A5FA`, `ColorPicker`/`AppsFolder` = `#A855F7` |
| n3 | `async` методы без `await` в `ActionService` — CS1998 warnings |
| n4 | `_flushCompleteTcs` — static internal поле вместо `InternalsVisibleTo` |
| n5 | `GetExecutablePath` фоллбэк на `"chrome.exe"` — упадёт если Chrome не на PATH |
| n6 | `BrushConverter` создаётся на каждый `SetBadgeColor` |
| n7 | Неконсистентный отступ в `AppSettingsWindow.xaml.cs:385-386` |
| n8 | `Margin="0"` и `FontWeight="Normal"` — мёртвый код в tooltip style |
| n9 | `GetPathFromRegistry` — silent `catch { return null; }` без логирования |
| n10 | `ActionServiceTests` — тест создаёт реальный `AppSettingsService` без temp dir override |

---

## Глубокий анализ по областям

### Clipboard Manager

**Что сделано хорошо:**
- Жизненный цикл событий: окно подписывается в `OnInitialized` и отписывается в `OnClosed`
- Симметрия `StartListening`/`StopListening` — корректная регистрация/снятие WndProc hook
- `SuppressNextChange()` паттерн предотвращает запись собственных clipboard-операций
- История переживает открытие/закрытие окна (service живёт на `ClipboardManagerUtility`)
- Защита от исключений в `OnClipboardChanged` — clipboard access известно капризен

**Требует исправления:**
- NRE на строке 148 (`text.Length` при null от `GetText()`)
- Нет лимита на изображения — memory bomb risk
- Дедупликация только по `Length` — пропускает разные изображения одной длины
- Очистка истории при shutdown (PII/пароли)
- Тесты structural (читают исходники как строки) — нулевое behavioral покрытие

### QR Code Generator

**Что сделано хорошо:**
- Чистое разделение между сервисом, моделями и UI
- Валидация длины на двух уровнях (UI `MaxLength` + сервис `ValidateText`)
- Fire-and-forget async с внутренним try-catch предотвращает unobserved exceptions
- Паттерн debounce через `CancellationTokenSource` для preview

**Требует исправления:**
- Синхронная генерация QR данных на UI thread — нужно offload в `Task.Run` или использовать `GenerateAsync`
- `EnsureRenderedArtifactsAsync` генерирует PNG + SVG даже когда нужен только один формат
- Bounds mismatch: сервис пускает pixelSize 1..100, UI слайдер 4..32
- Тесты не покрывают edge cases: color normalization, version calculation, cancellation

### MainWindow и панель

**Что сделано хорошо:**
- Централизованная layout-логика через `PanelLayoutHelper`
- Корректная работа `OverflowWrapPanel` для многострочных панелей
- Индикатор положения taskbar с фиксированными цветами по индексу

**Требует исправления:**
- Drag-and-drop state может устареть при `RefreshPanel` во время перетаскивания
- `OverflowWrapPanel` measure/arrange используют разные размеры для расчёта колонок
- `GetArrangedRectForIndex` дублирует логику Arrange — может рассинхронизироваться
- Двойной вызов `UpdateOrientation` в `RefreshPanel`

### Инфраструктура и сервисы

**Что сделано хорошо:**
- Lock-based синхронизация в `AppSettingsService`
- Корректная обработка ошибок в `HotkeyService`
- `ContextStateHelper` корректно нормализует контексты

**Требует исправления:**
- Утечка process handles в `ActionService` — `IActionProcessHandle` нигде не disposed
- `Dictionary<>` без синхронизации в `BrowserHelper` и `PathHelper`
- Race condition в `SaveAsync` — мутация внутри lock, сериализация вне lock
- `Logger.FlushQueue` — рекурсивный double-check под нагрузкой

### Tooltip Overhaul и UI

**Что сделано хорошо:**
- Transparent outer border с padding — стандартный WPF-паттерн для drop shadow
- Шрифт, цвета, отступы соответствуют ExecPlan
- `RecognizesAccessKey="False"` корректно отключает access key underline
- `TextRenderingMode="ClearType"` улучшает читаемость

**Требует внимания:**
- `BorderColor` используется как `Background` — семантическая ошибка
- `PanelBackground` потерял alpha channel
- Дублирующиеся цвета иконок снижают различимость в icon-only панели

---

## Глобальные проблемы

### Покрытие тестами

| Область | Статус |
|---------|--------|
| ClipboardManager | ❌ Только structural-тесты (чтение исходников) |
| QR Code edge cases | ⚠️ Happy path покрыт, edge cases нет |
| AppSettingsService.SaveAsync | ❌ Не тестирован |
| AppSettingsService.ReorderElements | ❌ Не тестирован |
| Logger.RotateLogFile failure | ❌ Не тестирован |
| ActionService.TryEnterFullscreenAsync | ❌ Не тестирован |
| Drag-and-drop во время RefreshPanel | ❌ Не тестирован |

### Thread Safety

| Поле | Проблема | Решение |
|------|----------|---------|
| `BrowserHelper._userDataPathOverrides` | `Dictionary<>` без sync | `ConcurrentDictionary` |
| `PathHelper._appDataFolderOverride` | static mutable без sync | `lock` или `Interlocked` |
| `ClipboardHistoryService._suppressNextChange` | plain `bool` | `volatile` или `Interlocked` |
| `AppSettingsService` race in SaveAsync | мутация внутри lock, сериализация вне | Serializable snapshot |

### Память и ресурсы

| Проблема | Решение |
|----------|---------|
| `CloneAppSettings` через JSON на каждый access | Dirty-flag или shallow clone |
| Clipboard images без лимита | `MaxImageBytes` константа, skip oversized |
| `IActionProcessHandle` не disposed | Using pattern или explicit dispose |
| `_flushCompleteTcs` static internal | `InternalsVisibleTo` атрибут |
| `SemaphoreSlim` в `AppSettingsService` не disposed | Реализовать `IDisposable` |

---

## Рекомендации по приоритету

### Must-fix (исправить до релиза)

1. **C1** — NRE в `ClipboardHistoryService.cs:148` — реальный runtime crash
2. **M5** — Утечка process handles в `ActionService` — накопление за сессию
3. **M1+M2** — Clipboard image handling (дедуп по длине + нет лимита) — memory bomb
4. **M9+M10** — Drag-and-drop crash при обновлении панели во время перетаскивания

### Should-fix (стоит исправить, но не блокирует релиз)

5. **C2** — JSON clone на каждом Settings access (performance)
6. **C3** — Recursive FlushQueue (edge case, но потенциально критичен)
7. **M6+M7** — Thread safety на static dictionaries (актуально для параллельных тестов)
8. **M4** — Race condition в SaveAsync
9. **M3** — Очистка clipboard history при shutdown

### Nice-to-have (можно в следующем релизе)

- Остальные minor/nit исправления
- Улучшение test coverage для Clipboard и QR Code edge cases
- Offload QR preview generation в background thread
- Устранение дублирования в `OverflowWrapPanel`/`PanelLayoutHelper`
- Оптимизация `TaskbarPositionIndicatorService` (z-order only on event)

---

---

## Итог исправлений

✅ Все **CRITICAL (3)** и **MAJOR (10)** проблемы исправлены!  
✅ Также исправлены **m1** (ToLower → ToLowerInvariant) и **m4** (surrogate pairs) из MINOR!  

### Сводка исправлений:
1. **Clipboard History (C1, M1, M2, M3, m1, m4):**
   - NRE при null text
   - Дедупликация изображений через SequenceEqual
   - Лимит MaxImageBytes = 5MB
   - Очистка истории при Dispose
   - ToLowerInvariant для поиска
   - Обработка surrogate pairs в DisplayText

2. **AppSettings Service (C2, M4):**
   - Ручной глубокий клон вместо JSON (снижение GC давления)
   - Снимок настроек внутри lock (race condition в SaveAsync)

3. **Logger (C3):**
   - Заменена рекурсия в FlushQueue на цикл

4. **Action Service (M5):**
   - Все IActionProcessHandle теперь обёрнуты в using

5. **BrowserHelper + PathHelper (M6, M7):**
   - ConcurrentDictionary для переопределённых путей
   - Lock для _appDataFolderOverride

6. **QR Code (M8):**
   - Генерация предпросмотра в Task.Run (не блокирует UI)

7. **Drag&Drop (M9, M10):**
   - RefreshPanel отменяет перетаскивание
   - Проверки валидности _draggedOriginalIndex

*Отчёт сформирован автоматически на основе 5 параллельных обзоров: ClipboardManager, QR Code Generator, MainWindow & Panel, Infrastructure & Services, Tooltip & UI.*
