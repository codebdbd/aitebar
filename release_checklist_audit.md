# SmartScreenDock — Релизный аудит чеклиста

> **Дата**: 2026-03-24  
> **Ревизия**: `7c89d7c` (master) + 2 uncommitted fixes  
> **Сборка**: .NET 8.0, WPF + WinForms NotifyIcon

---

## 1. Freeze и версия

| # | Пункт | Вердикт | Комментарий |
|---|-------|---------|-------------|
| 1.1 | Код-фриз объявлен | ⚠️ WARN | 2 файла ([Logger.cs](file:///b:/aitepanel/SmartScreenDock/Logger.cs), [MainWindow.xaml.cs](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs)) не закоммичены — это фиксы из [предыдущего ревью](file:///C:/Users/ostee/.gemini/antigravity/brain/a17d3acd-bb96-4668-859f-fdc2ec971621/walkthrough.md). Нужно закоммитить и после этого объявить фриз. |
| 1.2 | Версия релиза зафиксирована | ❌ FAIL | В [.csproj](file:///b:/aitepanel/SmartScreenDock/SmartScreenDock.csproj) нет `<Version>`, `<AssemblyVersion>`, `<FileVersion>`. [AssemblyInfo.cs](file:///b:/aitepanel/SmartScreenDock/AssemblyInfo.cs) содержит только `ThemeInfo`. Необходимо задать версию. |
| 1.3 | Changelog сформирован | ❌ FAIL | Файл changelog отсутствует в репозитории. |
| 1.4 | Релизный tag подготовлен | ❌ FAIL | `git tag` — пусто. Тег не создан. |

---

## 2. Сборка и артефакты

| # | Пункт | Вердикт | Комментарий |
|---|-------|---------|-------------|
| 2.1 | `dotnet restore` без ошибок | ✅ PASS | Все пакеты актуальны. |
| 2.2 | `dotnet build -c Debug` | ✅ PASS | 0 ошибок, 0 предупреждений. |
| 2.3 | `dotnet build -c Release` | ✅ PASS | 0 ошибок, 0 предупреждений. |
| 2.4 | Publish собирается | ✅ PASS | `dotnet publish -c Release` → [SmartScreenDock.exe](file:///b:/aitepanel/publish_test/SmartScreenDock.exe) (151 KB) + [.dll](file:///b:/aitepanel/publish_test/SmartScreenDock.dll) (440 KB). |
| 2.5 | Ресурсы в артефакте | ✅ PASS | [app.ico](file:///b:/aitepanel/SmartScreenDock/Resources/app.ico) и `Font Awesome 7 Brands-Regular-400.otf` включены как `<Resource>` в [.csproj](file:///b:/aitepanel/SmartScreenDock/SmartScreenDock.csproj) — встроены в DLL. |
| 2.6 | Запуск на чистой машине | 🔵 MANUAL | Требуется ручная проверка. Нужен .NET 8 Desktop Runtime. Убедиться, что Segoe Fluent Icons доступен (Win 11 по умолчанию, на Win 10 может не быть). |

---

## 3. Smoke-тест UI

| # | Пункт | Вердикт |
|---|-------|---------|
| 3.1 | Панель открывается из триггерной зоны | 🔵 MANUAL |
| 3.2 | Панель скрывается (клик вне) | 🔵 MANUAL |
| 3.3 | Кнопки: размеры/отступы/цвета | 🔵 MANUAL |
| 3.4 | Hover/active состояния | 🔵 MANUAL |
| 3.5 | Кнопка настроек → окно без ошибок | 🔵 MANUAL |
| 3.6 | Кнопка закрытия | 🔵 MANUAL |
| 3.7 | Трей-иконка (открыть, меню, выход) | 🔵 MANUAL |
| 3.8 | Нет висящих процессов после закрытия | 🔵 MANUAL |

> [!NOTE]
> Все пункты секции 3 — ручной smoke-тест. По коду: триггерная зона корректна (35%-65% экрана), анимация через `DoubleAnimation` с `CubicEase`, трей dispose'ится в [OnClosed](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#503-504), хук снимается через `Application.Exit`.

---

## 4. Функциональные сценарии кнопок

| # | Пункт | Вердикт | Комментарий |
|---|-------|---------|-------------|
| 4.1 | `Web`: URL / app mode / incognito | ✅ PASS (код) | `ArgumentList` используется корректно. |
| 4.2 | `Web`: профиль Chrome | ✅ PASS (код) | `--profile-directory=` добавляется через `ArgumentList`. |
| 4.3 | `Web`: ротация профилей | ✅ PASS (код) | [AdvanceProfile](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#368-378) сохраняет `LastUsedProfile` через [SaveConfig](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#298-311). |
| 4.4 | `Hotkey`: модификаторы + клавиша | ✅ PASS (код) | `SendInput` с корректным порядком down/up, модификаторы отпускаются в обратном порядке. |
| 4.5 | [Exe](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#379-457): запуск файла | ✅ PASS (код) | `UseShellExecute = true`, проверка `File.Exists`. |
| 4.6 | `ScriptFile`: запуск скрипта | ✅ PASS (код) | Аналогично [Exe](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#379-457). |
| 4.7 | `Command`: подтверждение + запуск | ✅ PASS (код) | [DarkDialog](file:///b:/aitepanel/SmartScreenDock/DarkDialog.xaml.cs#5-17) confirm → `ArgumentList` для cmd.exe. |
| 4.8 | Ошибки обрабатываются диалогом | ✅ PASS (код) | Все ветки в [ExecuteCustomAction](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#379-457) обёрнуты try-catch → [DarkDialog](file:///b:/aitepanel/SmartScreenDock/DarkDialog.xaml.cs#5-17). |

> 🔵 Все пункты требуют ручной верификации на реальных кнопках.

---

## 5. Настройки и сохранение данных

| # | Пункт | Вердикт | Комментарий |
|---|-------|---------|-------------|
| 5.1 | Добавление кнопки сохраняется | ✅ PASS (код) | [SaveElement](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#312-326) → [SaveConfig](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#298-311) → [RefreshPanel](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#237-297). |
| 5.2 | Редактирование сохраняется | ✅ PASS (код) | [SaveElement](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#312-326) с `removeId` удаляет старый, добавляет обновлённый. |
| 5.3 | Удаление кнопки | ✅ PASS (код) | `RemoveAll(x => x.Id == capturedId)` — по ID, не по индексу. |
| 5.4 | Двойной клик «Сохранить» | ✅ PASS (код) | Кнопка `saveButton.IsEnabled = false` в начале, восстанавливается в `finally`. |
| 5.5 | JSON валиден после операций | ✅ PASS (код) | `JsonSerializer.Serialize` с `WriteIndented = true`, `_saveSemaphore` предотвращает параллельную запись. |
| 5.6 | Состояние при перезапуске | ✅ PASS (код) | Чтение из `custom_buttons.json` в [RefreshPanel](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#237-297) при [Window_Loaded](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#213-235). |

---

## 6. Иконки и шрифты

| # | Пункт | Вердикт | Комментарий |
|---|-------|---------|-------------|
| 6.1 | Segoe Fluent Icons в каталоге | ✅ PASS (код) | Фильтрация через `GlyphTypeface.CharacterToGlyphMap` — показываются только реально существующие глифы. |
| 6.2 | Font Awesome Brands в каталоге | ✅ PASS (код) | Аналогично, фильтрация PUA (U+E000+), исключение обычных символов. |
| 6.3 | Поиск по коду и по имени | ✅ PASS (код) | Segoe: поиск по hex-коду. FA: `FontAwesomeNameAliases` — поиск по имени бренда. |
| 6.4 | Иконка отображается после сохранения | ✅ PASS (код) | `FontHelper.Resolve` корректно обрабатывает `pack://` URI. |
| 6.5 | Отсутствие шрифта | ⚠️ WARN | Segoe Fluent Icons — системный шрифт Win 11. На Win 10 каталог будет пуст (не крэш, но плохой UX). Нет предупреждения пользователю. |

---

## 7. Стабильность и обработка ошибок

| # | Пункт | Вердикт | Комментарий |
|---|-------|---------|-------------|
| 7.1 | Нет unhandled exceptions | ✅ PASS (код) | Все `async void` обработчики обёрнуты в try-catch. [MouseHookCallback](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#170-194) защищён. |
| 7.2 | Fire-and-forget с обработкой | ✅ PASS (код) | [LoadChromeProfilesAsync](file:///b:/aitepanel/SmartScreenDock/SettingsWindow.xaml.cs#149-207) имеет `.ContinueWith(OnlyOnFaulted)` для логирования. |
| 7.3 | Нет deadlock при сохранении | ✅ PASS (код) | `SemaphoreSlim(1,1)` с `WaitAsync` — неблокирующий, deadlock невозможен. |
| 7.4 | Логирование работает | ✅ PASS (код) | `Logger.Log` создаёт директорию, пишет в `error.log`, thread-safe через `lock`. |
| 7.5 | Ротация логов | ✅ PASS (код) | При > 1 MB → переименование в `.bak`, старый `.bak` удаляется. |

---

## 8. Безопасность и корректность запуска процессов

| # | Пункт | Вердикт | Комментарий |
|---|-------|---------|-------------|
| 8.1 | Нет опасной конкатенации аргументов | ✅ PASS | Везде используется `ProcessStartInfo.ArgumentList`. |
| 8.2 | Используется `ArgumentList` | ✅ PASS | Web: `ArgumentList.Add(...)`, Command: `ArgumentList.Add("/c")` + `ArgumentList.Add(el.ActionValue)`. |
| 8.3 | Команды через подтверждение | ✅ PASS | `ActionType.Command` всегда показывает [DarkDialog](file:///b:/aitepanel/SmartScreenDock/DarkDialog.xaml.cs#5-17) confirm. |
| 8.4 | Нет инъекций | ✅ PASS | `ArgumentList` делегирует экранирование .NET runtime. |

---

## 9. Ресурсы и жизненный цикл

| # | Пункт | Вердикт | Комментарий |
|---|-------|---------|-------------|
| 9.1 | Хуки снимаются | ✅ PASS | `Application.Current.Exit += (_, _) => UninstallMouseHook()`. |
| 9.2 | NotifyIcon dispose | ✅ PASS | [OnClosed](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#503-504) вызывает `_notifyIcon?.Dispose()`. Также в пункте «Выход» трей-меню. |
| 9.3 | Process dispose | ⚠️ WARN | В `ActionType.Web` с `IsTopmost` — `Process.Start(psi)` → `proc` **не** dispose'ится (задержка до 5s для `SetWindowPos`). Для [Exe](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs#379-457)/`ScriptFile` — корректно `using`. Для `Command` — `Process.Start(psiCmd)` возвращённый объект **не** dispose'ится. |
| 9.4 | Нет утечек при длительной работе | 🔵 MANUAL | `DispatcherTimer` (30ms) не создаёт объекты. Хук один экземпляр. Проверить через Task Manager. |

---

## 10. Репозиторий и релизная чистота

| # | Пункт | Вердикт | Комментарий |
|---|-------|---------|-------------|
| 10.1 | Нет лишних бинарников | ✅ PASS | [.zip](file:///b:/aitepanel/aitepanel_r6.zip)/[.rar](file:///b:/aitepanel/aitepanel_icons_v8.rar) не отслеживаются ([.gitignore](file:///b:/aitepanel/.gitignore)). |
| 10.2 | [.gitignore](file:///b:/aitepanel/.gitignore) покрывает артефакты | ✅ PASS | Включены: `bin/`, `obj/`, `*.user`, `.vs/`, `*.zip`, `*.rar`, `*.7z`, `publish/`. |
| 10.3 | Нет незакоммиченных изменений | ❌ FAIL | 2 modified: [Logger.cs](file:///b:/aitepanel/SmartScreenDock/Logger.cs), [MainWindow.xaml.cs](file:///b:/aitepanel/SmartScreenDock/MainWindow.xaml.cs) (37 ins, 24 del — исправления из предыдущего ревью). |
| 10.4 | CI/проверки green | ⚠️ WARN | CI не настроен. Локальные сборки Debug/Release — green. |

> [!WARNING]
> В git отслеживаются 3 внутренних файла, которые не должны быть в релизе:
> - [SmartScreenDock/codex_instructions_fixes_r5.md](file:///b:/aitepanel/SmartScreenDock/codex_instructions_fixes_r5.md)
> - [SmartScreenDock/codex_release_code_review_prompt.md](file:///b:/aitepanel/SmartScreenDock/codex_release_code_review_prompt.md)
> - [SmartScreenDock/ui_parameters.md](file:///b:/aitepanel/SmartScreenDock/ui_parameters.md)
> 
> Рекомендуется удалить из отслеживания (`git rm --cached`) и добавить `*.md` в [.gitignore](file:///b:/aitepanel/.gitignore) проекта, либо удалить сами файлы.

---

## 11. Документация релиза

| # | Пункт | Вердикт | Комментарий |
|---|-------|---------|-------------|
| 11.1 | Пользовательская инструкция | ❌ FAIL | Нет файла README или guide. |
| 11.2 | Known issues / ограничения | ❌ FAIL | Не задокументированы (Win 10 без Segoe Fluent Icons, зависимость от Chrome). |
| 11.3 | Rollback-план | ❌ FAIL | Не подготовлен. |

---

## 12. Финальный gate

| # | Пункт | Вердикт |
|---|-------|---------|
| 12.1 | Critical дефекты: 0 | ✅ PASS |
| 12.2 | High дефекты: 0 | ⚠️ — см. ниже |
| 12.3 | Все обязательные пункты закрыты | ❌ FAIL |
| 12.4 | Вердикт | **NO-GO** |
| 12.5 | Ответственный / дата | Не зафиксировано |

---

## Сводка блокеров (NO-GO)

Критических дефектов в коде **нет**. Однако релиз заблокирован по организационным пунктам:

| # | Блокер | Приоритет | Действие |
|---|--------|-----------|----------|
| 1 | Нет версии в проекте | High | Добавить `<Version>1.0.0</Version>` в [.csproj](file:///b:/aitepanel/SmartScreenDock/SmartScreenDock.csproj) |
| 2 | 2 файла не закоммичены | High | `git add . && git commit` |
| 3 | Нет git tag | Medium | `git tag v1.0.0` после коммита |
| 4 | Нет changelog | Medium | Создать `CHANGELOG.md` |
| 5 | Внутренние [.md](file:///b:/aitepanel/SmartScreenDock/ui_parameters.md) в git | Low | `git rm --cached` + [.gitignore](file:///b:/aitepanel/.gitignore) |
| 6 | Нет README/документации | Medium | Создать минимальный README |
| 7 | Process не dispose в 2 местах | Low | Добавить `using`/`?.Dispose()` |

> [!TIP]
> Если блокеры 1–3 будут закрыты — можно перейти к **GO** при условии успешного ручного smoke-теста (секция 3).
