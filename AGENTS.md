# AiteBar Agent Handbook

## ExecPlans

When writing complex features or significant refactors, use an ExecPlan (as described in PLANS.md) from design to implementation.

When creating or changing an ExecPlan, follow `PLANS.md` completely. An ExecPlan must be self-contained, living, understandable to a novice, and must describe demonstrably working behavior. Keep `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` current as work proceeds; record decisions and surprising findings in the plan instead of relying on chat history.

## О проекте

`AiteBar` — desktop-утилита для Windows: скрываемая edge-панель быстрого доступа с кнопками пользовательских действий, встроенными утилитами, контекстами и системной интеграцией через tray/hotkeys.

Репозиторий содержит:
- основное приложение `AiteBar`
- тестовый проект `AiteBar.Tests`
- артефакты publish/installer в `artifacts`
- скрипт сборки инсталлятора в `installer`

## Стек и архитектура

- Платформа: `.NET 10`, `net10.0-windows`
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
- основная логика UI и системного поведения остается в `MainWindow`, но часть обработчиков вынесена в partial-файлы `MainWindow.*.cs`
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
dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll
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
- При добавлении или изменении встроенной утилиты синхронизировать связанные места: `UnifiedButtonService`, `Models`, `AppSettingsService`, `ActionService`/`UtilityRegistry`, XAML настроек, локализации `Resources/Strings*.resx`, README/docs/functions и focused tests для non-UI логики.

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

- `SettingsWindow` сейчас является компактной single-form разметкой для параметров одной кнопки, без фактического `TabControl`.
- Не превращать `SettingsWindow` в перегруженный мастер-настройщик приложения; общие параметры, hotkeys, контексты и порядок встроенных утилит должны оставаться в `AppSettingsWindow`.
- Если возвращается вкладчатая структура `SettingsWindow`, делать это как отдельное осознанное UI-изменение с проверкой размера окна и сценариев создания/редактирования кнопки.

### UI-изменения

- Если изменение затрагивает геометрию, отступы, размеры или позиционирование панели, проверять не только XAML, но и связанную кодовую логику в `MainWindow.xaml.cs`, `MainWindow.*.cs` partial handlers и helper-ах расчета.
- Не оставлять “временные” визуальные решения без проверки на всех ориентациях панели.

### Quick Note

- Не ломать Quick Note как легкое single-note окно поверх текущей архитектуры `QuickNoteUtility`, `QuickNoteWindow`, `QuickNoteService`, `QuickNoteMarkdown`, `QuickNoteDocumentHelper`, `QuickNoteLayoutHelper` и `QuickNoteTheme`.
- При правках Quick Note сохранять сценарии: pin toggle удерживает окно открытым при потере фокуса, unpinned-режим закрывает окно при потере фокуса, размер и позиция запоминаются и clamp-ятся в рабочую область монитора.
- Не ломать Undo/Redo через `Ctrl+Z`/`Ctrl+Y` и видимые команды окна.
- Не ломать открытие URL обычным кликом, обработку conflict-copy, статус для длинной заметки при ограничении link highlighting и round-trip underline через Markdown-compatible `<u>...</u>`.
- Форматирование, которое доступно в toolbar, должно сохраняться после save/reload; если меняется Markdown serialization/parsing, добавлять focused unit-тесты.

### Clipboard Manager

- Не ломать Clipboard Manager как легкую runtime-history утилиту поверх `ClipboardManagerUtility`, `ClipboardManagerWindow` и `ClipboardHistoryService`.
- При правках сохранять сценарии: подписка на `WM_CLIPBOARDUPDATE`, копирование записи обратно в clipboard без немедленного дублирования, очистка истории, поиск/фильтрация, текстовые и image-записи.
- Не увеличивать без явной причины лимиты истории и текста (`MaxEntries`, `MaxTextLength`), чтобы не создавать скрытое потребление памяти и privacy-риск.
- Clipboard history не должна становиться persistent storage без отдельного решения по privacy, настройкам очистки и документации пользователя.

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
dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll
```

4. Если менялся UI панели или настройки, вручную проверить:
- показ панели
- скрытие панели
- позиционирование панели
- все 4 стороны панели: `Top`, `Bottom`, `Left`, `Right`
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

## Release, CI и документация

### Актуальные источники

При расхождении между этим файлом и проектными файлами считать источниками правды сами проектные файлы:
- [README.md](README.md) — возможности, установка и release quality summary.
- [CHANGELOG.md](CHANGELOG.md) — история изменений и release notes.
- [docs/technical-reference.md](docs/technical-reference.md) — стек, команды, CI/CD и техническая справка.
- [docs/architecture.md](docs/architecture.md) — архитектура приложения и workflow.
- [docs/functions.md](docs/functions.md) — карта функций.
- [docs/USER_MANUAL.md](docs/USER_MANUAL.md) — пользовательское поведение.
- [docs/SENTRY_SETUP.md](docs/SENTRY_SETUP.md) — включение Sentry и приоритет конфигурации.

Не добавлять в `AGENTS.md` ссылки на файлы, которых нет в репозитории. Не фиксировать здесь быстро устаревающие числа вроде количества тестов или текущей версии без явной необходимости.

### CI и качество

- `.github/workflows/build-test.yml` собирает `Release`, запускает тесты, публикует coverage summary и сохраняет coverage artifact для push/PR в `main` и `master`.
- `.github/workflows/codeql.yml` запускает CodeQL security-and-quality analysis для C#.
- `.github/dependabot.yml` обновляет NuGet и GitHub Actions зависимости.
- Lock-файлы пакетов должны оставаться согласованными с проектами; не обновлять их случайно.
- Если добавляется новая non-UI логика, покрывать ее unit-тестами. UI-сценарии, которые трудно автоматизировать, описывать в ручной проверке.

### Release guardrails

- Версия release tag `vX.Y.Z` должна совпадать с `<Version>` в `AiteBar/AiteBar.csproj`.
- При изменении версии синхронизировать `AiteBar/AiteBar.csproj`, `AiteBar/AssemblyInfo.cs`, installer metadata и `CHANGELOG.md`, если соответствующие файлы участвуют в релизе.
- `.github/workflows/release.yml` собирает installer, проверяет changelog section, требует один непустой installer artifact и публикует `SHA256SUMS.txt`.
- Code signing включается только при наличии GitHub Secrets `WINDOWS_SIGNING_CERT_BASE64` и `WINDOWS_SIGNING_CERT_PASSWORD`; без них installer остается unsigned.
- Built-in update check открывает GitHub Release page после URL validation. Auto-install не реализовывать без отдельного решения по signing и безопасному обновлению.

### Sentry и privacy

- Sentry отключен по умолчанию.
- Production/support включение: `AITEBAR_SENTRY_DSN` или `SENTRY_DSN`.
- Тестовое включение возможно через `%APPDATA%\Codebdbd\Aite Bar\settings.json`, как описано в `docs/SENTRY_SETUP.md`.
- Не включать отправку PII по умолчанию. Любые изменения telemetry/privacy должны быть отражены в `docs/USER_MANUAL.md` и `docs/SENTRY_SETUP.md`.

### Backlog-направления

- Повышать coverage постепенно, только после добавления тестов для реальной non-UI логики.
- Рассмотреть SonarQube или расширение текущих CodeQL rules/quality gates.
- Поддерживать существующий `.editorconfig` и не расходиться с текущим стилем кода.
- API documentation, встроенный auto-updater и usage analytics считать отдельными крупными задачами, требующими ExecPlan.

## UI Contract

### Visual Style Guidelines

1. **Цветовая палитра**: Использовать приглушенные темные тона (Background: `#1A1A1C`, Panels: `#252526`). Избегать чрезмерного контраста.
2. **Акценты**: Основной акцентный цвет — профессиональный синий (`#007ACC`).
3. **Компоновка**: `AppSettingsWindow` должно оставаться компактным и разделенным на логические вкладки (`Tabs`); `SettingsWindow` должно оставаться компактной формой редактирования одной кнопки.
4. **Отсутствие скролла**: Основные интерфейсы должны вписываться в фиксированную высоту окна без вертикальной прокрутки всего окна. Локальный scroll внутри перегруженной вкладки допустим, если иначе окно становится слишком высоким.
5. **Закругления**: Использовать `CornerRadius="4"` для кнопок и полей ввода, и `6-8` для панелей.

### Locked Layout Invariants

1. `SettingsWindow`: Должно оставаться компактной single-form разметкой редактирования одной кнопки; не переносить в него общие настройки приложения.
2. `MainWindow`: Панель должна сохранять минималистичный вид и плавную анимацию появления.
