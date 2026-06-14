# AiteBar — Что ещё нужно обновить

> Дата: 2025-06-14
> Основа: анализ кодовой базы, тестового покрытия, документации и сделанных исправлений

---

## Содержание

1. [Тесты — пробелы покрытия](#1-тесты--пробелы-покрытия)
2. [Документация — что устарело](#2-документация--что-устарело)
3. [Код — оставшиеся улучшения](#3-код--оставшиеся-улучшения)
4. [Ручное тестирование](#4-ручное-тестирование)
5. [Приоритеты](#5-приоритеты)

---

## 1. Тесты — пробелы покрытия

### Источники без тестов (non-UI логика)

| # | Класс | Файл | Что проверить | Сложность |
|---|---|---|---|---|
| T1 | `UtilityRegistry` | `UtilityRegistry.cs` | `Register` — dedup по Id, `GetById` — поиск, `UtilityBase<T>.LaunchAsync` — singleton window (окно уже видимо → Activate, иначе → CreateWindow), callback invocation, cleanup при Close | Medium |
| T2 | `HotkeyKeyCatalog` | `HotkeyKeyCatalog.cs` | Каталог содержит ожидаемые клавиши, нет дубликатов, display names не пустые | Low |
| T3 | `OverflowWrapPanel` | `OverflowWrapPanel.cs` | `GetCapacity`, `MeasureVertical`, `MeasureHorizontal`, `ArrangeVertical`, `ArrangeHorizontal` — layout math. Аналогично `PanelLayoutHelperTests` | Medium |
| T4 | `ActionService.ContainsPotentiallyDangerousCommandSyntax` | `ActionService.cs:287-306` | Regex покрывает `del`, `format`, `shutdown`, `rm`, `bcdedit`, `diskpart`, `cipher`. Не покрывает безопасные команды (`calc`, `explorer`, `notepad`). Операторы `&`, `|`, `>`, `<` | Low |
| T5 | `ActionService.BuildCommandConfirmationMessage` | `ActionService.cs:276-285` | Опасные команды получают предупреждение, безопасные — нет | Low |
| T6 | `AppSettingsService.WriteSettingsWithBackupAsync` | `AppSettingsService.cs:214-277` | Temp-файл создаётся, `File.Replace` атомарен, cleanup temp при ошибке | Medium |
| T7 | `Logger.BuildLogEntry` | `Logger.cs:47-55` | Newlines заменяются на ` | `, сообщение безопасно | Trivial |
| T8 | `Logger.LogAsync` | `Logger.cs:32-33` | Запускает `Log` в фоне, не блокирует вызывающий поток | Low |
| T9 | `PanelPackageManifest` (serialization) | `PanelPackageManifest.cs` | JSON round-trip: `FormatVersion`, `ExportedAt`, `Elements` — десериализация/сериализация без потери данных | Low |
| T10 | `NativeIntegrationService` lifecycle | `NativeIntegrationService.cs` | `InstallMouseHook` / `UninstallMouseHook` / `Dispose` — не бросает исключений, `MouseDownOutside` event подписан/отписан | Low |

### Приоритет

```
Высокий:  T1 (UtilityRegistry), T4-T5 (dangerous commands regex), T6 (backup atomicity)
Средний:  T3 (OverflowWrapPanel), T9 (manifest serialization)
Низкий:   T2, T7, T8, T10
```

---

## 2. Документация — что устарело

### Требуют обновления

| # | Файл | Что устарело | Что обновить |
|---|---|---|---|
| D1 | `CHANGELOG.md` | Нет записи о security fix (P2: dangerous command warnings), async flush (P4), backup atomicity (P9), CancellationToken (L4) | Добавить секцию `[Unreleased]` с исправлениями |
| D2 | `docs/architecture.md` | Описание `AppSettingsService.Save()` не учитывает `WriteSettingsWithBackupAsync` + `File.Replace` pattern | Обновить описание persistence layer |
| D3 | `docs/architecture.md` | Не описан `ComputePanelMetrics()` / `CalculateAvailableSize()` — новые методы в MainWindow | Обновить секцию layout calculation |
| D4 | `docs/technical-reference.md` | Не описаны новые константы `Constants.AnimationFadeMs`, `Constants.AnimationSlideMs` | Добавить описание констант анимаций |
| D5 | `AGENTS.md` | Пункт "Self-check" не упоминает проверку 4 сторон панели после layout-изменений | Добавить в чеклист |

### Не нуждаются в обновлении

- `docs/functions.md` — актуален
- `docs/USER_MANUAL.md` — актуален
- `docs/SENTRY_SETUP.md` — актуален
- `docs/UTILITIES.md` — актуален

---

## 3. Код — оставшиеся улучшения

### Безопасные (низкий риск)

| # | Что | Где | Описание |
|---|---|---|---|
| C1 | Удалить dead code | `MainWindow.xaml.cs:788` | `int visibleSystemButtonCount = GetVisibleSystemButtonCount()` — не используется в `ApplyPanelSizeConstraints(metrics)` |
| C2 | Заменить `Logger.Log` на `LogAsync` в async-контекстах | `MainWindow.xaml.cs` — ~15 мест | Строки 553, 640, 1020, 1106, 1135, 1183, 1302, 1746, 2454, 2559, 2672, 2677, 2704, 2713 — все в catch-блоках sync-методов, оставить как есть. Но в async-контекстах (строки 1396, 2399, 2689) — уже заменены. Проверить остальные async catch-блоки | Low |
| C3 | Централизовать версию | `Directory.Build.props`, `AiteBar.csproj`, `AssemblyInfo.cs` | Перенести `<Version>` в `Directory.Build.props`, убрать из `.csproj`. Требует проверки `Assembly.GetExecutingAssembly().GetName().Version` | Low |

### Средние (средний риск)

| # | Что | Где | Описание |
|---|---|---|---|
| C4 | Extract `PanelPositionHelper` | `MainWindow.xaml.cs` → `PanelPositionHelper.cs` | Вынести `GetDockCoordinates`, `GetClosestDockEdge`, `FindScreenIndex` — чистая математика, без UI-зависимостей | Medium |
| C5 | Extract `ContextSwitchHelper` | `MainWindow.xaml.cs` → `ContextSwitchHelper.cs` | Вынести `FindNextContextIndex` — общий код в 4 методах переключения контекстов | Medium |
| C6 | Timer visibility | `MainWindow.xaml.cs:1367` | Не запускать `_timer` до первого `ShowDock`. Проблема: остановка при скрытой панели ломает hover-to-show. Альтернатива: не запускать в `EnsureStartupInfrastructure`, запускать в `ShowDock` | Medium |

### Высокие (высокий риск, не сейчас)

| # | Что | Где | Описание |
|---|---|---|---|
| C7 | Diff-based RefreshPanel | `MainWindow.xaml.cs:1470-1591` | Не пересоздавать кнопки каждый раз, а обновлять существующие. Требует изменения model layer | High |
| C8 | DI-контейнер | Весь проект | `Microsoft.Extensions.DependencyInjection` вместо ручной компоновки | High |

---

## 4. Ручное тестирование

### После изменений layout (P5)

Все проверки делать на **каждой стороне** (Top, Bottom, Left, Right):

| # | Сценарий | Ожидаемый результат |
|---|---|---|
| M1 | Панель Top, 0 кнопок | Панель показывается, системные утилиты видны, разделители скрыты |
| M2 | Панель Top, 5 кнопок | Кнопки в 1 ряд, разделители на месте |
| M3 | Панель Top, 15 кнопок | Кнопки переносятся на 2 ряда (overflow wrap) |
| M4 | Панель Left, 15 кнопок | Вертикальный layout, 2 колонки при overflow |
| M5 | Drag handle → смена стороны | Панель корректно позиционируется на новой стороне |
| M6 | Смена монитора через drag | MonitorIndex обновляется, панель на новом мониторе |
| M7 | Context switch колёсиком | Кнопки переключаются, анимация slide работает |
| M8 | Context switch через контекстное меню | Кнопки обновляются, context menu корректно отображается |
| M9 | Drag-and-drop reorder кнопок | Кнопки меняют порядок, анимация slide работает |
| M10 | Панель Top → resize окна настроек | Панель не дёргается, позиция стабильна |

### После security fixes (P2)

| # | Сценарий | Ожидаемый результат |
|---|---|---|
| M11 | Кнопка с командой `calc.exe` | Подтверждение без предупреждения |
| M12 | Кнопка с командой `del /q file.txt` | Подтверждение **с** предупреждением `Action_CommandDangerWarning` |
| M13 | Кнопка с командой `shutdown /s` | Подтверждение **с** предупреждением |
| M14 | Кнопка с командой `echo hello` | Подтверждение без предупреждения |

### После backup fixes (P9)

| # | Сценарий | Ожидаемый результат |
|---|---|---|
| M15 | Настроить кнопку, закрыть приложение | `settings.json` обновлён, `settings.json.backup.0` содержит предыдущую версию |
| M16 | Забить диск до 0 байт, сохранить настройки | temp-файл удаляется, `settings.json` не повреждён |
| M17 | 5+ сохранений подряд | Бэкапы ротируются, oldest удаляется |

---

## 5. Приоритеты

```
Фаза 1 (сейчас):     C1 (dead code), D1-D2 (changelog + architecture)
Фаза 2 (неделя):      T4-T5 (regex tests), T6 (backup tests), C2 (LogAsync audit)
Фаза 3 (2 недели):    C4-C5 (extract helpers), T1 (UtilityRegistry), T3 (OverflowWrapPanel)
Фаза 4 (месяц):       D3-D5 (оставшаяся документация), C6 (timer visibility)
Фаза 5 (когда-нибудь): C7-C8 (diffing, DI)
```

---

## Итого

| Категория | Всего | Высокий приоритет | Средний | Низкий |
|---|---|---|---|---|
| Тесты | 10 | 3 (T1, T4, T5) | 2 (T3, T6) | 5 |
| Документация | 5 | 2 (D1, D2) | 2 (D3, D4) | 1 (D5) |
| Код | 8 | 0 | 3 (C4, C5, C6) | 5 |
| Ручное тестирование | 17 | 10 (M1-M10) | 4 (M11-M14) | 3 (M15-M17) |
| **Итого** | **40** | **15** | **11** | **14** |
