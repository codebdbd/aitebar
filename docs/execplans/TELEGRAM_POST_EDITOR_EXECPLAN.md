# Реализация встроенной утилиты TelegramPostEditor и серверного планировщика AiteBar

Этот ExecPlan является живым документом. Разделы `Progress`, `Surprises & Discoveries`, `Decision Log` и `Outcomes & Retrospective` должны поддерживаться в актуальном состоянии по мере выполнения работы. Документ оформлен и поддерживается в соответствии с `PLANS.md` из корня репозитория.

## Purpose / Big Picture

После завершения работы пользователь AiteBar сможет открывать встроенную утилиту `TelegramPostEditor` с панели быстрого доступа или по горячей клавише, создавать и редактировать форматированные посты для Telegram-каналов с медиа-вложениями и шаблонами, обрабатывать тексты с помощью ИИ через уже настроенный в AiteBar `AiGateway` и собственную библиотеку версионированных инструкций, проверять превью и лимиты под актуальный Telegram Bot API, отправлять тестовые и основные публикации в один или несколько каналов сразу либо ставить их в надёжную очередь на лёгкий сервер `TelegramPostEditor.Server` на Linux VPS, который продолжит публикации при выключенном Windows-ПК.

Работоспособность подтверждается прохождением сборки `dotnet build AiteBar.sln -c Release`, модульных и интеграционных тестов `AiteBar.Tests` и `TelegramPostEditor.Server.Tests`, сборкой установщика `installer/Build-Installer.ps1` и выполнением приёмочных сценариев `AT-01...AT-30` из `telegram_post_editor_aitebar_tz_v2.md`.

## Progress

- [x] (2026-11-15 03:08Z) Этап 0: проведена сверка актуального репозитория (`AiteBar.sln`, `AiteBar.csproj`, `UtilityRegistry`, `UtilityButtonCatalog`, `DarkWindow`, `AiGateway`, `WindowsAiCredentialStore`, `BackupService`, `Resources/Strings*.resx`).
- [x] (2026-11-15 03:20Z) Этап 1: каркас утилиты `TelegramPostEditor`, регистрация, окно `Workspace`, локализация (EN/RU/UK/DE), настройки видимости и горячих клавиш, пакет `Microsoft.Data.Sqlite` и 4 теста интеграции (пройдены).
- [x] (2026-11-15 03:23Z) Этап 2: локальная SQLite (WAL, миграции), модель `PostDocument`, сериализация `FlowDocument` / Telegram entities, `PostRepository`, `PostAutosaveService`, `PostMediaStore` (SHA-256), шаблоны и 9 тестов (пройдены).
- [x] (2026-11-15 03:27Z) Этап 3: библиотека инструкций (`PostInstructionService`), интеграция с `AiGateway` (`PostAiService`), сравнение моделей и 12 тестов (пройдены).
- [x] (2026-11-15 03:30Z) Этап 4: `TelegramSecretStore`, `TelegramBotClient`, каналы, превью, валидатор лимитов, тестовая отправка и мультипубликация без дублей (15 тестов пройдены).
- [x] (2026-11-15 03:34Z) Этап 5: `TelegramPostEditor.Server` (Minimal API, pairing, AES-256-GCM, SQLite WAL, `PublicationWorker` с lease/retry/`UNKNOWN_RESULT`, systemd/backup) и `PublicationSyncService`, 2 серверных теста (пройдены).
- [x] (2026-11-15 01:44Z) Этап 6: UI `TelegramPostEditorWindow`, `PostBackupService`, интеграция в `BackupService`, `PostBackupTests`, обновление `docs/functions.md`; финальная сборка `AiteBar.csproj` и `AiteBar.Tests.csproj` в Release (0 ошибок), `TelegramPostEditor` (16/16) и `TelegramPostEditor.Server.Tests` (2/2) пройдены.

## Surprises & Discoveries

- Observation: В `Directory.Build.props` включено `RestorePackagesWithLockFile=true`, а в `AiteBar/AiteBar.csproj` пакет `Microsoft.Data.Sqlite` ещё не подключён.
  Evidence: Проверка `Directory.Build.props` (строка 3) и `AiteBar/AiteBar.csproj`. При добавлении пакета потребуется обновление `AiteBar/packages.lock.json`.
- Observation: В `AiteBar/Resources/` присутствуют 4 файла локализации (`Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, `Strings.de.resx`), а не только 3.
  Evidence: Список файлов в `AiteBar/Resources/`. Все 4 файла нужно синхронно обновлять.
- Observation: `MainWindow` хранит общий экземпляр `_aiGateway` и предоставляет `GetAiGateway()`, тогда как `TextProcessingUtility` и `PromptBuilderUtility` создают свой `new AiGateway(settingsService)`.
  Evidence: `AiteBar/MainWindow.xaml.cs` (строки 46, 128, 202). Для сохранения 15-минутного кэша моделей предпочтительно использовать `owner is MainWindow main ? main.GetAiGateway() : new AiGateway(settingsService)`.

## Decision Log

- Decision: Включить серверные проекты `TelegramPostEditor.Server` (`net10.0`) и `TelegramPostEditor.Server.Tests` в `AiteBar.sln`.
  Rationale: Единый solution упрощает CI и проверку сборки всего кода, не затрагивая `dotnet publish AiteBar/AiteBar.csproj` и `installer/Build-Installer.ps1`.
  Date/Author: 2026-11-15 / Approved by User.
- Decision: Использовать `WindowsAiCredentialStore` с изолированным префиксом `AiteBar:TelegramBot:` / `AiteBar:TelegramServer:` и расширить `BackupService` для сохранения токенов в `private.bin` только при `IncludeSecrets=true`.
  Rationale: Переиспользует проверенный DPAPI/Credential Manager и AES-GCM механизм `BackupService` без смешивания с ИИ-ключами и без записи секретов в открытый JSON.
  Date/Author: 2026-11-15 / Approved by User.

## Milestones

### Milestone 1: Каркас утилиты и интеграция с AiteBar

В конце вехи в AiteBar появится зарегистрированная утилита `TelegramPostEditor`, открывающая полноразмерное окно `DarkWindow` (`WindowClass = Workspace`) с трёхпанельным макетом (библиотека, редактор, предпросмотр), кнопкой в `UtilityButtonCatalog`, переключателем видимости и горячей клавишей в `AppSettingsWindow`, а также локализацией на 4 языках. В `AiteBar.csproj` будет добавлен `Microsoft.Data.Sqlite`. Проверка: `dotnet build AiteBar.sln -c Release` и тесты в `AiteBar.Tests/TelegramPostEditor/`.

### Milestone 2: Локальная SQLite, PostDocument, редактор и медиа

В конце вехи появится изолированная БД SQLite WAL в `%AppData%/Codebdbd/Aite Bar/TelegramPostEditor/posts.db` с миграциями, структурированная модель `PostDocument`, двусторонняя сериализация `FlowDocument` / `TelegramDocumentSerializer`, автосохранение с дебаунсом, папки/теги/корзина/поиск, `PostMediaStore` с SHA-256 и шаблоны постов.

### Milestone 3: Библиотека инструкций и интеграция с AiGateway

В конце вехи появятся `PostInstructionService` (версионированные инструкции, привязка к каналу/посту, экспорт/импорт) и `PostAiService` поверх `AiGateway` с потоковой генерацией, отдельной областью предложений/диффом и сравнением моделей по явному подтверждению.

### Milestone 4: Telegram Bot API, превью, валидатор и мультипубликация

В конце вехи появятся `TelegramSecretStore`, `TelegramBotClient`, `TelegramChannelService`, `TelegramValidationService` (актуальные лимиты, разбивка/сокращение), `TelegramPreviewService`, тестовая отправка и мультипубликация без дублей.

### Milestone 5: Серверный планировщик и календарь/очередь

В конце вехи появятся проекты `TelegramPostEditor.Server` и `TelegramPostEditor.Server.Tests` (Minimal API, pairing, AEAD AES-256-GCM, SQLite WAL, `BackgroundService` с lease/retry/`UNKNOWN_RESULT`, systemd) и клиентский календарь/очередь/синхронизация.

### Milestone 6: Бэкап, экспорт/импорт, журнал, документация и выпуск

В конце вехи будут завершены интеграция с `BackupService`, архивный экспорт/импорт с manifest/SHA-256, раздел «Журнал», обновление `README.md` / `docs/functions.md` / `docs/USER_MANUAL.md`, релизная сборка и прохождение `AT-01...AT-30`.

## Outcomes & Retrospective

- **Реализовано**:
  - Встроенная утилита `TelegramPostEditor` (`UtilityBase<TelegramPostEditorWindow>`, `DarkWindow` с `WindowClass = Workspace`), регистрация в `UtilityRegistry` / `UtilityButtonCatalog` / `HotkeyService` / `AppSettingsWindow`, локализация на 4 языках (`Strings*.resx`).
  - Локальное хранилище SQLite WAL (`PostDbMigrator`, `PostRepository`, `PostAutosaveService`, `PostMediaStore` с дедупликацией SHA-256), модель `PostDocument`, двусторонняя сериализация `FlowDocument` / UTF-16 `TelegramMessageEntity` (`TelegramDocumentSerializer`).
  - Библиотека версионированных инструкций (`PostInstructionService`) и потоковый AI-помощник поверх `AiGateway` (`PostAiService`) с областью предложений и сравнением моделей.
  - Telegram Bot API клиент (`TelegramBotClient`), изолированное хранение токенов (`TelegramSecretStore`), управление каналами (`TelegramChannelService`), валидация лимитов (`TelegramValidationService`), превью (`TelegramPreviewService`) и публикация (`PublicationService`).
  - Серверный планировщик `TelegramPostEditor.Server` (Minimal API, pairing, AES-256-GCM, SQLite WAL, `PublicationWorker`, systemd unit / backup script) и клиентская синхронизация (`PublicationSyncService`).
  - Архивный экспорт/импорт (`PostBackupService`) и интеграция с `BackupService` (включая токены ботов в `private.bin` при `IncludeSecrets=true`).
  - Документация в `docs/functions.md`.
- **Результаты проверок**:
  - `dotnet build .\AiteBar\AiteBar.csproj -c Release --no-restore`: 0 ошибок (2 предупреждения NU1903 о транзитивном `SQLitePCLRaw.lib.e_sqlite3 2.1.11` из `Microsoft.Data.Sqlite 10.0.0`).
  - `dotnet build .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore -p:ReleaseVerificationRoot=...`: 0 ошибок, 0 предупреждений.
  - `dotnet vstest ... --TestCaseFilter:"FullyQualifiedName~AiteBar.Tests.TelegramPostEditor"`: 16/16 пройдены.
  - `dotnet vstest .\TelegramPostEditor.Server.Tests\bin\Release\net10.0\TelegramPostEditor.Server.Tests.dll`: 2/2 пройдены.
  - Полный прогон `AiteBar.Tests.dll` в изолированном `.verify` каталоге: 1751 пройдены, 5 пропущены (`QuickNoteSnapIntegrationTests`), 13 упали из-за привязки к `AppContext.BaseDirectory` при сборке в `.verify` (`QuickNoteFormattingControlsTests` искал `.verify\AiteBar\QuickNoteWindow.xaml`, `TextProcessingWindowLayoutTests` — `.verify\AiteBar\TextProcessingWindow.xaml.cs`).
- **Остаётся для ручной / живой проверки**:
  - Живые сценарии с реальным Telegram Bot API, внешними AI-провайдерами и развёртыванием `TelegramPostEditor.Server` на Linux VPS (из набора `AT-01...AT-30`).
  - Ручная UI-проверка панели AiteBar (показ/скрытие, 4 стороны, hotkeys, tray) и сборка установщика `.\installer\Build-Installer.ps1`.
