# AiteBar Agent Handbook

# ExecPlans

When writing complex features or significant refactors, use an ExecPlan (as described in PLANS.md) from design to implementation.

## О проекте

`AiteBar` — desktop-утилита для Windows: скрываемая edge-панель быстрого доступа с кнопками пользовательских действий, встроенными утилитами, контекстами и системной интеграцией через tray/hotkeys.

Репозиторий содержит:
- основное приложение `AiteBar`
- тестовый проект `AiteBar.Tests`
- артефакты publish/installer в `artifacts`
- скрипт сборки инсталлятора в `installer`

## Стек и архитектура

- Платформа: `.NET 8`, `net8.0-windows`
- UI: `WPF`
- Системная интеграция: `Windows Forms NotifyIcon` и Win32 interop
- Solution: `AiteBar.sln` с двумя проектами:
  - `AiteBar`
  - `AiteBar.Tests`
- Тестовый стек:
  - `xUnit`
  - `Microsoft.NET.Test.Sdk`
  - `coverlet.collector`
- Поставка:
  - `dotnet publish`
  - `Inno Setup`
  - сборка инсталлятора через `installer/Build-Installer.ps1`

Текущая архитектурная практика:
- основная логика UI и системного поведения сосредоточена в `MainWindow`
- чистую расчетную или нормализующую логику лучше выносить в отдельные helper-классы
- для layout панели нужно использовать централизованную layout-математику, а не локальные правки контейнеров “по месту”

## Ключевые команды

### Сборка

```powershell
dotnet build .\AiteBar.sln -c Release
```

### Тесты

Основной вариант:

```powershell
dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
```

Fallback для случаев, когда WPF/MSBuild временно ломает `dotnet test` на `wpftmp`/`obj`:

```powershell
dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll
```

### Сборка инсталлятора

```powershell
.\installer\Build-Installer.ps1
```

Важно:
- скрипт сам вызывает `dotnet publish`, если не передан `-SkipPublish`
- publish уходит в `artifacts\publish\win-x64`
- installer уходит в `artifacts\installer`

## Правила внесения изменений

### Общие

- Не придумывать новые стандарты поверх текущего проекта. Опираться на уже существующую структуру и стиль.
- Не рассинхронизировать версии приложения, publish и installer.
- Если логика может быть протестирована отдельно от UI, добавлять или обновлять unit-тесты.

### MainWindow и панель

- Не ломать минималистичный вид панели `MainWindow`.
- Не ломать плавную анимацию появления/скрытия панели.
- Смена стороны панели теперь поддерживается напрямую на рабочей панели через drag-and-drop за handle; при правках `MainWindow` нужно сохранять этот сценарий и проверять, что после drop корректно сохраняются и край, и монитор.
- Изменения layout панели вносить через централизованную layout-логику и расчетные helper-ы, а не случайными правками `WrapPanel`/`DockPanel`/`Margin`.
- При изменениях панели обязательно проверять все 4 стороны:
  - `Top`
  - `Bottom`
  - `Left`
  - `Right`
- При изменениях контекстов проверять переключение, перенос кнопок между контекстами и поведение панели на коротком и длинном контексте.

### SettingsWindow

- Не убирать вкладчатую структуру `SettingsWindow`.
- Параметры кнопки и управление порядком должны оставаться логически разделенными вкладками.

### UI-изменения

- Если изменение затрагивает геометрию, отступы, размеры или позиционирование, проверять не только XAML, но и связанную кодовую логику в `MainWindow.xaml.cs` и helper-ах расчета.
- Не оставлять “временные” визуальные решения без проверки на всех ориентациях панели.

## Самопроверка перед завершением работы

### Обязательный чеклист

1. Собрать `Release`:

```powershell
dotnet build .\AiteBar.sln -c Release
```

2. Прогнать тесты:

```powershell
dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
```

3. Если `dotnet test` падает из-за WPF/MSBuild temp-файлов (`wpftmp`, `obj`, `*.g.cs`), прогнать fallback:

```powershell
dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll
```

4. Если менялся UI панели или настройки, вручную проверить:
- показ панели
- скрытие панели
- позиционирование панели
- поведение контекстов
- hotkeys
- доступ к функциям из tray

5. Если менялись версия, publish-логика, installer-логика или пути артефактов:

```powershell
.\installer\Build-Installer.ps1
```

После этого проверить, что актуальный инсталлятор действительно лежит в:

```text
artifacts\installer
```

## Release Quality & 2026 Best Practices

На конец Q2 2026 проведена комплексная оценка проекта на соответствие лучшим практикам:

### 📊 Текущее состояние (v1.6.1)

- ✅ **Архитектура**: 8/10 — четкое разделение на слои, SOLID принципы соблюдаются
- ✅ **Безопасность**: 8/10 — проведен pre-release audit, все HIGH issues исправлены
- ✅ **Тестирование**: 7/10 — 99 unit-тестов, coverage summary/artifact и baseline threshold в CI
- ✅ **Документация**: 7/10 — README, CHANGELOG, USER_MANUAL, но нет API docs
- ⚠️ **CI/CD**: 6/10 — GitHub Actions build/test, release workflow и CodeQL добавлены; нужен staging/dry-run proof
- ⚠️ **Мониторинг**: 3/10 — Sentry SDK интегрирован dev/support-only через env vars
- ⚠️ **Обновления**: 5/10 — GitHub Releases check-and-open с URL validation; auto-install не реализован до signing

**Итог**: Готов к production (7/10), но отстает от стандартов 2026 года в автоматизации.

### 📋 Документация анализа

Подробный анализ находится в папке `docs/`:
- [RELEASE-2026-SUMMARY.md](docs/RELEASE-2026-SUMMARY.md) — краткая summary таблица (начните отсюда)
- [RELEASE-HARDENING-CHANGE-PLAN.md](docs/RELEASE-HARDENING-CHANGE-PLAN.md) — актуальные release-hardening follow-ups

### 🔴 Приоритет-1 действия (Q3 2026)

1. **Проверить release workflow dry-run/staging запуском** — 1-2 часа
   - `workflow_dispatch` dry-run без GitHub Release
   - проверка installer artifact, `release_notes.md` и `SHA256SUMS.txt`

2. **Подготовить code signing certificate** — отложено до появления бюджета
   - после покупки настроить GitHub Secrets `WINDOWS_SIGNING_CERT_BASE64` и `WINDOWS_SIGNING_CERT_PASSWORD`
   - проверить подпись на staging/dry-run release

3. **Повышать coverage постепенно**
   - baseline threshold уже включен в CI
   - повышать порог только после добавления тестов для non-UI логики

### 🟡 Приоритет-2 действия (Q4 2026)

1. Повысить test coverage до 75%
2. Добавить SonarQube/CodeQL интеграцию
3. Создать .editorconfig для стиля кода

### 🟢 Приоритет-3 действия (2027+)

1. API документация (DocFX)
2. Встроенный auto-updater (NetSparkle)
3. Usage analytics

Подробнее см. [RELEASE-2026-SUMMARY.md](docs/RELEASE-2026-SUMMARY.md).

## UI Contract

### Visual Style Guidelines

1. **Цветовая палитра**: Использовать приглушенные темные тона (Background: `#1A1A1C`, Panels: `#252526`). Избегать чрезмерного контраста.
2. **Акценты**: Основной акцентный цвет — профессиональный синий (`#007ACC`).
3. **Компоновка**: Окно настроек должно быть компактным и разделенным на логические вкладки (`Tabs`).
4. **Отсутствие скролла**: Основные интерфейсы должны вписываться в фиксированную высоту окна без использования вертикальной прокрутки всего контента.
5. **Закругления**: Использовать `CornerRadius="4"` для кнопок и полей ввода, и `6-8` для панелей.

### Locked Layout Invariants

1. `SettingsWindow`: Должно сохранять вкладчатую структуру для разделения параметров кнопки и управления порядком.
2. `MainWindow`: Панель должна сохранять минималистичный вид и плавную анимацию появления.
