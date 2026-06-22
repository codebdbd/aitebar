# AiteBar — Pre-Release Code Review v5

Дата: 2026-06-22
Сборка: ✅ Release (0 ошибок, 0 warnings) | Тесты: 7 FAIL / 512+ total

---

## Статус тестов (7 FAIL)

| # | Тест | Тип ошибки |
|---|------|------------|
| 1 | `AppSettingsWindowIntegrationTests.LanguageSelection_PersistsUiCultureImmediately` | Test expects `private async void CmbLanguage_SelectionChanged` — method is sync `private void` |
| 2 | `ClipboardManagerIntegrationTests.ClipboardManager_IsWiredIntoPanelSettingsAndUtilityRegistry` | Test expects `_settings.ShowPresetClipboardManager = Chk...` — actual uses local `settings` |
| 3 | `FileSorterWindowLayoutTests.FileSorterHeader_CloseButtonIsNotClippedAndHasNoFocusOutlineTrigger` | `BtnClose` no longer exists in FileSorterWindow.xaml (removed during simplification) |
| 4 | `MainWindowIconConverterOrientationTests.HorizontalPanel_KeepsCurrentContextRowHeight_WhenAnotherContextIsWider` | `root.MinWidth` equals 220, test asserts `> 220` — boundary/real layout bug |
| 5 | `IconConverterWindowLayoutTests.Window_MinimumSize_DoesNotClipCriticalControlsInRussian` | XAMLParseException: `resources/app.ico` not found in test context |
| 6 | `IconConverterIntegrationTests.IconConverter_IsWiredIntoPanelSettingsAndUtilityRegistry` | Same `_settings` vs `settings` variable name mismatch |
| 7 | `LocalizationServiceTests.XamlTextProperties_DoNotContainTranslatableLiteralText` | `256 px`, `16 px`, `32 px`, `48 px` in IconConverterWindow.xaml are hardcoded |

### Типичные причины

- **#1, #2, #3, #6** — тесты устарели после рефакторинга кода. Код был изменён (sync вместо async, локальные переменные вместо полей, UI упрощён), а тесты не обновлены.
- **#4** — возможен реальный баг layout: `PanelLayoutHelper` не резервирует ширину из более широкого контекста для горизонтальной панели.
- **#5** — тест создаёт `IconConverterWindow` вне App-контекста, ресурс `app.ico` недоступен. Нужен `try-catch` или `SkippableFact`.
- **#7** — pixel-size лейблы (`256 px` и т.д.) — технические единицы, не требующие локализации. Нужно добавить в `allowedTechnicalText`.

---

## CRITICAL (нужно исправить до релиза)

### 1. `Logger.cs:149` — Ротация логов уничтожает всю историю

```csharp
catch (Exception ex)
{
    Debug.WriteLine(ex);
    File.WriteAllText(LogPath, string.Empty);  // ВСЯ ИСТОРИЯ УДАЛЕНА
}
```

Если `File.Move` падает (антивирус, блокировка, права), catch обрезает лог-файл до пустоты. Часы/диагностика пропадают.

**Fix:** Убрать `File.WriteAllText(LogPath, string.Empty)`. Оставить файл как есть, залогировать ошибку.

### 2. `AppSettingsService.cs:60` — `UpdateSettings` отбрасывает изменения элементов

```csharp
var next = CloneAppSettings(_appSettings);   // 57
next.Elements = [.. _elements];              // 58
update(next);                                 // 59: delegate мутирует next
next.Elements = [.. _elements];              // 60: ПЕРЕЗАПИСАТЬ оригинальными!
```

Строка 60 отменяет любые изменения `Elements`, которые delegate мог сделать.

**Fix:** Убрать строку 60 или задокументировать, что elements не обновляются через delegate.

### 3. Тесты не проходят (7 FAIL)

См. таблицу выше. Некоторые из них — устаревшие тесты (#1, #2, #3, #6, #7), некоторые — возможные баги (#4).

**Fix:**
- #1: Обновить тест: заменить `private async void` на `private void`, обновить expectations.
- #2, #6: Обновить тесты: `_settings` → `settings` (локальная переменная).
- #3: Обновить или удалить тест — `BtnClose` удалён, тест устарел.
- #4: Investigate — возможно баг в `PanelLayoutHelper`, возможно нужно изменить assertion.
- #5: Добавить try-catch или SkipIf для WPF-тестов вне App-контекста.
- #7: Добавить `"256 px"`, `"16 px"`, `"32 px"`, `"48 px"` в `allowedTechnicalText`.

---

## HIGH (рекомендуется исправить)

### 4. `TelemetryService.cs:35` — `_initialized = true` до фактической инициализации

```csharp
lock (SyncRoot)
{
    if (_initialized) return;
    _initialized = true;    // 35: УСТАНОВЛЕН
}
// ... 38-95: фактическая работа (может упасть/early-return)
```

Если Sentry SDK не инициализируется — повторный вызов сразу вернётся. Telemetry отключена на всю сессию.

**Fix:** Перенести `_initialized = true` после фактической инициализации.

### 5. `LocalizationService.cs:194-195` — Глобальная смена культуры

```csharp
CultureInfo.CurrentCulture = culture;
CultureInfo.CurrentUICulture = culture;
CultureInfo.DefaultThreadCurrentCulture = culture;      // глобально
CultureInfo.DefaultThreadCurrentUICulture = culture;    // глобально
```

Влияет на все потоки (скачивание favicon, JSON-сериализация).

**Fix:** Убрать `DefaultThreadCurrentCulture` и `DefaultThreadCurrentUICulture`. Оставить только текущий поток.

### 6. `MainWindow.xaml.cs:122-126` — Ошибки hotkey регистрация игнорируются

```csharp
private void OnSettingsChanged(object? sender, EventArgs e)
{
    UnregisterGlobalHotkey();
    RegisterGlobalHotkey();     // возвращает ошибки — ИГНОРИРУЕТСЯ
}
```

**Fix:** Залогировать или показать пользователю ошибки регистрации.

### 7. `MainWindow.xaml.cs:1523` — `SourceElement!` без null-проверки

```csharp
return BuildElementContextMenu(item.SourceElement!);
```

Если `SourceElement == null` (повреждённые настройки) — `NullReferenceException`.

**Fix:** Добавить null-check.

### 8. `ClipboardHistoryService.cs:197-198` — `SequenceEqual` на полных byte arrays

```csharp
entry.ImageBytes.SequenceEqual(imageBytes)  // до 5 МБ × 50 записей
```

При каждом изменении clipboard — сравнение до 250 МБ данных.

**Fix:** Сравнивать `Length` первым, затем хеш первых 1024 байт.

---

## MEDIUM

| # | Файл:Строка | Проблема |
|---|-------------|----------|
| 9 | `MainWindow.xaml.cs:8` | `_timer` 30ms polling (~33fps) для проверки позиции курсора. 100-200ms достаточно. |
| 10 | `QuickNoteWindow.xaml.cs:116-119` | `_saveTimer` и `_geometrySaveTimer` не останавливаются в `OnClosed`. |
| 11 | `ClipboardManagerWindow.xaml.cs:87` | Полная пересборка UI (`Children.Clear()`) при каждом keystroke в поиске. |
| 12 | `MainWindow.xaml.cs:662,673` | `Process.Start` без Dispose — хендл не освобождается. |
| 13 | `PanelLayoutHelper.cs:256-266` | 3-диапазонный cap обрезает кнопки без индикатора overflow. |
| 14 | `ScreenColorPickerWindow.cs:43-51` | Full screen capture на UI-потоке в конструкторе (~99 МБ на 3x4K). |
| 15 | `Logger.cs:139` | Ротация бэкапов по `CreationTimeUtc` — не меняется при move/copy. |
| 16 | `UnifiedButtonService.cs:51` | Deep clone `AppSettings` только для чтения `UtilityButtonOrder`. |
| 17 | `TelemetryService.cs:98-124` | Чтение settings файла параллельно с записью через `AppSettingsService`. |

---

## LOW

| # | Файл:Строка | Проблема |
|---|-------------|----------|
| 18 | `ActionService.cs:159-177` | Мёртвый код: проверка browser-not-found недостижима в hotkey-handler. |
| 19 | `MainWindow.xaml.cs:337-350 vs 352-372` | Дублирование `SwitchActiveContextAsync` и `ActivateContextRelative`. |
| 20 | `ActivationZoneHelper.cs:29-30` | Off-by-one: последний пиксель не в зоне активации. |
| 21 | `PanelPositionHelper.cs:46` | LINQ `OrderBy().First()` для 4 элементов. |
| 22 | `PathHelper.cs:49-52` | Redundant `Directory.Exists` перед `CreateDirectory`. |
| 23 | `OverflowWrapPanel.cs:114-126` | Новый `List<UIElement>` в каждом Measure/Arrange. |
| 24 | `AppSettingsWindow.xaml.cs:488-492` | `BrushConverter` на каждый вызов `GetPanelBadgeBrush`. |
| 25 | `NativeIntegrationService.cs:10-72` | Finalizer для Win32 hook — ненадёжен на другом потоке. |
| 26 | `AppSettingsService.cs:439-476` | Temp file leak при crash процесса. |
| 27 | `Models.cs:141-145` | Неконсистентные отступы. |

---

## Сводка

| Категория | Кол-во | Статус |
|-----------|--------|--------|
| Critical | 3 | Исправить до релиза |
| High | 5 | Рекомендуется |
| Medium | 9 | Желательно |
| Low | 10 | Опционально |
| **Итого** | **27** | |

---

## Топ-5 приоритетов перед релизом

1. **Тесты (7 FAIL)** — Обновить устаревшие тесты (#1-#3, #5-#7), investigate layout-баг (#4).
2. **Logger.cs:149** — Убрать `File.WriteAllText(LogPath, string.Empty)`.
3. **AppSettingsService.cs:60** — Убрать `next.Elements = [.. _elements]` или задокументировать.
4. **TelemetryService.cs:35** — Перенести `_initialized = true` после инициализации.
5. **ClipboardHistoryService.cs:197** — Заменить `SequenceEqual` на быстрое сравнение.

---

## Код-ревью v4 — Статус исправлений

Из CODE_REVIEW_v4.md исправлено: **0 из 32**.

Все пункты из v4 (Critical #1-2, High #3-6, Medium #7-22, Low #23-32) остаются актуальными и не исправлены. Рекомендуется приоритезировать исправления перед релизом.
