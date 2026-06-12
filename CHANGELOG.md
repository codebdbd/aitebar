# Changelog

🇷🇺 Все значительные изменения проекта будут документироваться в этом файле.

🇬🇧 All notable changes to this project will be documented in this file.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/),
проект придерживается [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.7.7] - 2026-06-12

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Утилита конвертации иконок**: Встроенная утилита для конвертации изображений и SVG в формат ICO с поддержкой нескольких размеров и настроек
- **Icon converter utility**: Built-in utility for converting images and SVG to ICO format with multi-size support and customization options
- **Помощник по разметке QuickNote**: Реализован QuickNoteLayoutHelper для расчета позиций окна и работы с геометрией
- **QuickNote layout helper**: Implemented QuickNoteLayoutHelper for calculating window positions and geometry
- **Новые тесты**: Добавлены тесты для FileSorterService, QuickNoteService, QuickNoteLayoutHelper и QuickNoteMarkdown
- **New tests**: Added tests for FileSorterService, QuickNoteService, QuickNoteLayoutHelper, and QuickNoteMarkdown
- **Документация**: Добавлены документы AiteBar Panel Exec Plan и Contract Clean
- **Documentation**: Added AiteBar Panel Exec Plan and Contract Clean documentation

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Улучшено качество масштабирования иконок**: Использован высококачественный кубический ресемплер (Mitchell) вместо линейного фильтра
- **Improved icon scaling quality**: Used high-quality cubic resampler (Mitchell) instead of linear filter
- **Рефакторинг IconConverterService**: Устранено дублирование кода, улучшена управляемость ресурсами
- **IconConverterService refactoring**: Removed code duplication, improved resource management
- **Исправлены тесты WPF**: Добавлен WpfTestCollection для корректной инициализации приложения в тестах
- **Fixed WPF tests**: Added WpfTestCollection for proper app initialization in tests
- **Рефакторинг QuickNote**: Извлечена логика разметки в QuickNoteLayoutHelper, улучшена стабильность
- **QuickNote refactoring**: Extracted layout logic to QuickNoteLayoutHelper, improved stability
- **Безопасность FileSorter**: Добавлены ограничения по размеру файлов, проверки путей, подтверждение записи и повторные попытки
- **FileSorter security**: Added file size limits, path checks, write verification, and retry logic

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Размытые иконки в конвертере**: Исправлено качество масштабирования при создании иконок
- **Blurry icons in converter**: Fixed scaling quality when generating icons
- **Проблемы с многопоточностью в тестах MainWindow**: Устранены ошибки доступа к Application.Current из разных потоков
- **Multi-threading issues in MainWindow tests**: Fixed errors accessing Application.Current from different threads
- **Сохранение геометрии QuickNote**: Добавлено отслеживание позиции и размера окна QuickNote, сохранение между сессиями
- **QuickNote geometry saving**: Added tracking of QuickNote window position and size, persisted across sessions
- **UX QuickNote**: Исправлено взаимодействие с окном, отмена/повтор действий, обновление состояния меню конфликтов
- **QuickNote UX**: Fixed window interaction, undo/redo actions, conflict menu state updates
- **Безопасность FileSorter**: Добавлена защита от символических ссылок, повторные попытки при ошибках доступа
- **FileSorter security**: Added protection against symlinks, retries on access errors

## [1.7.6] - 2026-06-07

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Палитра цветов в диалоге добавления кнопок**: обновлена палитра до 20 цветов, расположенных в 4 ряда по 5, скрыт произвольный HEX ввод
- **Color palette in button add dialog**: updated palette to 20 colors arranged in 4 rows of 5, hidden custom HEX input

### 🇷🇺 Удалено | 🇬🇧 Removed
- **Глобальные горячие клавиши для пользовательских кнопок**: функция была временно удалена для стабилизации
- **Global hotkeys for custom buttons**: feature was temporarily removed for stabilization

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Рефакторинг системы утилит**: вынесены утилиты в отдельные классы с интерфейсом `IUtility` и `UtilityRegistry`
- **Utility system refactoring**: utilities extracted to separate classes with `IUtility` interface and `UtilityRegistry`
- **Поведение фокуса клавиатуры на панели**: клавиатурный режим теперь не включается автоматически, активируется только при нажатии Tab/стрелок
- **Panel keyboard focus behavior**: keyboard mode no longer activates automatically, only on Tab/arrow keys
- **Нормализация элементов**: создается копия вместо изменения входного объекта для безопасности
- **Element normalization**: creates a copy instead of modifying input object for safety
- **Логирование исключений**: добавлено логирование в `AppSettingsService` и `PathHelper`
- **Exception logging**: added logging in `AppSettingsService` and `PathHelper`
- **Поиск исполняемых файлов**: `FindExecutableOnPath` теперь возвращает `null` при отсутствии файла
- **Executable file search**: `FindExecutableOnPath` now returns `null` when file not found

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Фокус при открытии панели через горячую клавишу**: клавиатурный режим теперь включается правильно
- **Focus when opening panel via hotkey**: keyboard mode now activates correctly

## [1.7.5] - 2026-06-04

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Поддержка горячих клавиш для пользовательских кнопок**: теперь каждой кнопке можно назначить глобальную горячую клавишу, которая будет выполнять действие кнопки.
- **Hotkeys support for custom buttons**: Every custom button can now have a global hotkey assigned to trigger its action.
- **Отдельная горячая клавиша запуска кнопки**: global shortcut пользовательской кнопки хранится отдельно от сочетания, которое отправляет Hotkey-действие.
- **Separate button activation hotkey**: A custom button's global shortcut is stored separately from the combination sent by a Hotkey action.
- **Расширенная валидация горячих клавиш**: проверка зарезервированных системой сочетаний (Win+E, Win+R, Win+L и другие), конфликтов между клавиш и поддерживаемых сочетаний.
- **Enhanced hotkey validation**: Checks for system-reserved combinations (Win+E, Win+R, Win+L and others), hotkey conflicts and supported key validity.
- **Приоритет командных горячих клавиш**: системные горячие клавиши (ShowPanel, QuickNote и другие) имеют приоритет над пользовательскими кнопками.
- **Command hotkey priority**: System hotkeys (ShowPanel, QuickNote, etc.) now take priority over custom button hotkeys.
- **Выборочная блокировка горячих клавиш**: при открытых окнах разрешены горячие клавиши QuickNote и TimerStopwatch, чтобы не блокировать их при открытых окнах.
- **Selective hotkey blocking**: QuickNote and TimerStopwatch hotkeys are allowed even when owned windows are open.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Переработка HotkeyService**: добавлен динамический пул ID для горячих клавиш пользовательских элементов.
- **HotkeyService refactoring**: Added dynamic hotkey ID pool for user element hotkeys.
- **Автоперерегистрация горячих клавиш**: горячие клавиши перерегистрируются автоматически при изменении настроек без перезапуска приложения.
- **Hotkey auto-registration**: Hotkeys are now automatically re-registered when settings change without restarting the app.
- **Централизованный каталог клавиш**: списки доступных клавиш для global shortcuts и Hotkey-действий используют общий источник.
- **Centralized key catalog**: Available key lists for global shortcuts and Hotkey actions now use a shared source.
- **Автоперерегистрация кнопок**: горячие клавиши пользовательских кнопок обновляются сразу после сохранения, удаления, дублирования и импорта.
- **Button hotkey re-registration**: Custom button hotkeys now update immediately after save, delete, duplicate, and import.
- **ExecuteHotkey**: добавлена проверка состояния клавиш перед эмуляцией, обработка ошибок SendInput, аварийное освобождение модификаторов и 30-мс задержки между нажатиями.
- **ExecuteHotkey**: Added key state checks before simulation, SendInput failure handling, emergency modifier release, and 30ms delays between presses.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Самозапуск Hotkey-кнопок**: отправляемое сочетание больше не может повторно запускать ту же кнопку через её global shortcut.
- **Hotkey button self-triggering**: A sent combination can no longer trigger the same button again through its global shortcut.
- **Возможные застревания клавиш** при эмуляции горячих клавиш из-за отсутствия проверки состояния и аварийного освобождения модификаторов.
- **Possible stuck keys** when simulating hotkeys caused by missing key state checks and missing emergency modifier release.
- **Надежность SendInput**: неполная отправка ввода теперь возвращает ошибку.
- **SendInput reliability**: Partial input delivery now returns a failure.
- **Диагностика регистрации hotkey**: предупреждения теперь показывают причину отказа Windows или конфликта.
- **Hotkey registration diagnostics**: Warnings now include the Windows rejection or conflict reason.
- **Неполная валидация горячих клавиш** при регистрации (не проверялись системные сочетания и конфликты).
- **Incomplete hotkey validation** that did not check system-reserved combinations and conflicts.
- **Нет перерегистрации горячих клавиш** при изменении настроек (требовалось перезапускать приложение).
- **Missing hotkey re-registration** after settings changes, which previously required an app restart.
- **Лимит размера настроек**: слишком большие settings-файлы отклоняются до чтения.
- **Settings size limit**: Oversized settings files are rejected before reading.

### 🇷🇺 Тесты | 🇬🇧 Tests
- **Полное покрытие тестами HotkeyService**, включая динамические ID, элементы и валидацию.
- **Full test coverage for HotkeyService**, including dynamic IDs, element hotkeys and validation.
- **Обновлены тесты HotkeyServiceTests** для новой логики.
- **Updated HotkeyServiceTests** for the new logic.
- **Regression-покрытие клавиатуры**: добавлены тесты миграции, import/export, предотвращения самозапуска, ошибок SendInput и очистки модификаторов.
- **Keyboard regression coverage**: Added tests for migration, import/export, self-trigger prevention, SendInput failures, and modifier cleanup.

## [1.7.4] - 2026-06-01

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Таймер и секундомер**: Встроенная утилита быстрых инструментов для запуска таймера с пресетами и своего времени, секундомера, паузы, сброса, компактного режима и звука окончания.
- **Timer and stopwatch**: Built-in quick tool for timer presets and custom time entry, stopwatch mode, pause, reset, compact mode and completion sound.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Проверка обновлений без автоустановки**: До появления подписи инсталлятора проверка обновлений снова открывает страницу релиза GitHub вместо скачивания и запуска `.exe` из приложения.
- **Update check without auto-install**: Until installer signing is available, update checking opens the GitHub release page instead of downloading and launching `.exe` from the app.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Таймер и секундомер**: При закрытии окна работающий таймер теперь останавливается и отписывается от `DispatcherTimer`, чтобы закрытое окно не удерживалось обработчиком тиков.
- **Timer and stopwatch**: Closing the window now stops the running timer and unsubscribes from `DispatcherTimer` so the closed window is not retained by tick handlers.

### 🇷🇺 Тесты | 🇬🇧 Tests
- **Релизные проверки**: Убраны nullable warnings в тестах и пересобран установщик для версии `1.7.4`.
- **Release checks**: Removed nullable warnings from tests and rebuilt the installer for version `1.7.4`.

## [1.7.3] - 2026-05-31

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Quick Note без глобального mouse hook**: Окно заметки больше не устанавливает отдельный low-level mouse hook; закрытие при потере фокуса переведено на WPF `Deactivated`, чтобы не влиять на движение мыши.
- **Quick Note without global mouse hook**: The note window no longer installs its own low-level mouse hook; focus-loss closing now uses WPF `Deactivated` to avoid interfering with mouse movement.

- **Позиционирование Quick Note**: Увеличен отступ от края экрана, чтобы заметка не перекрывала рабочую панель.
- **Quick Note positioning**: Increased edge clearance so the note window does not overlap the dock panel.

- **Тестируемая логика заметок**: Расчет позиции и работа с `FlowDocument`/`TextPointer` вынесены в helper-классы.
- **Testable note logic**: Position calculation and `FlowDocument`/`TextPointer` handling moved into helper classes.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Зависание Quick Note**: Оптимизирован поиск позиции в тексте заметки для подсветки ссылок и операций форматирования.
- **Quick Note hangs**: Optimized text position lookup used by link highlighting and formatting operations.

- **Поведение мыши**: Устранен второй глобальный перехват мыши при открытой заметке, который мог вызывать рывки и задержки курсора.
- **Mouse behavior**: Removed the second global mouse interception while Quick Note is open, which could cause cursor lag or erratic movement.

### 🇷🇺 Тесты | 🇬🇧 Tests
- **Quick Note coverage**: Добавлены тесты для Markdown, файлового сервиса заметок, тем, расчета координат и `FlowDocument` helper-ов.
- **Quick Note coverage**: Added tests for Markdown, note file service, themes, coordinate calculation and `FlowDocument` helpers.

## [1.7.2] - 2026-05-30

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Система резервных копий настроек**: Автоматическое создание бэкапов (до 5 версий) при каждом сохранении настроек и восстановление из бэкапов при повреждении основного файла
- **Backup system for settings**: Automatic backup creation (up to 5 versions) on each settings save and restore from backups when main file is corrupted

- **Расширенное тестовое покрытие**: Добавлено более 100 новых тестов для модулей `ActionExecutionResult`, `BrowserHelper`, `Constants`, `EasingHelper`, `FontHelper`, `IconHelper`, `LocalizationService`, `PanelPackageMapper`, `QuickNoteService` и `TelemetryService`
- **Extended test coverage**: Added over 100 new tests for `ActionExecutionResult`, `BrowserHelper`, `Constants`, `EasingHelper`, `FontHelper`, `IconHelper`, `LocalizationService`, `PanelPackageMapper`, `QuickNoteService` and `TelemetryService` modules

### 🇷🇺 Изменено | 🇬🇧 Changed
- **UI кнопки "Добавить"**: Полностью переработан дизайн кнопки "Add" с круглым фоном и улучшенными состояниями hover/active
- **Add button UI**: Complete redesign of Add button with round background and improved hover/active states

- **Обработка файлов настроек**: Улучшена устойчивость к поврежденным и слишком большим файлам настроек (не бросает исключения, логирует предупреждения)
- **Settings file handling**: Improved resilience to corrupted and oversized settings files (no exceptions thrown, warnings logged)

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Обновление внутренних методов**: Методы работы с бэкапами в `AppSettingsService` сделаны `internal` для тестирования
- **Internal methods update**: Backup-related methods in `AppSettingsService` made `internal` for testing

## [1.7.1] - 2026-05-29

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Анимации панелей и Quick Note**: Реализована новая система анимаций с централизованными константами и easing-функциями
- **Panel and Quick Note animations**: Implemented new animation system with centralized constants and easing functions

- **Компонент Settings Dropdown**: Анимация открытия списка изменена с Fade на Slide для лучшего UX
- **Settings Dropdown component**: Opening animation changed from Fade to Slide for better UX

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Разделитель после кнопки "Добавить"**: Разделитель теперь отображается всегда, если есть пользовательские кнопки (включая вторые панели без системных утилит)
- **Separator after Add button**: Separator now always appears if there are user buttons (including secondary panels without system utilities)

- **Расчет размеров панели**: Обновлена логика расчета размеров для корректного отображения разделителей
- **Panel size calculation**: Updated size calculation logic for proper separator display

## [1.7.0] - 2026-05-28

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Автоматический установщик**: Теперь при проверке обновлений пользователю предлагается загрузить и установить новую версию автоматически прямо из приложения.
- **Automatic installer**: Users can now download and install new versions automatically directly from the application when checking for updates.

- **Загрузка инсталлятора**: Поддержка прямого скачивания инсталлятора (.exe) из GitHub Releases с индикатором прогресса.
- **Installer download**: Support for direct installer (.exe) download from GitHub Releases with progress indicator.

- **Сетевая телеметрия**: Интегрирована Sentry SDK для отслеживания ошибок и исключений в dev/support-only режиме через переменные окружения.
- **Network telemetry**: Integrated Sentry SDK for error and exception tracking in dev/support-only mode via environment variables.

- **Security warning при установке**: Перед установкой неподписанного инсталлятора пользователю показывается предупреждение о возможном SmartScreen warning (P1 blocker).
- **Security warning on install**: Users are warned about possible SmartScreen warning before installing unsigned installer (P1 blocker).

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Диалог проверки обновлений**: При наличии инсталлятора показываются три опции: "Download and install", "Open GitHub release page", "Cancel".
- **Update check dialog**: When installer is available, three options are shown: "Download and install", "Open GitHub release page", "Cancel".

- **Обработка ошибок**: Улучшена обработка сетевых ошибок при проверке обновлений и скачивании инсталлятора с информативными сообщениями пользователю.
- **Error handling**: Improved network error handling during update checking and installer download with informative user messages.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- Обеспечена надежность автоустановщика с корректной обработкой исключений и cleanup временных файлов при ошибках.
- Ensured auto-installer reliability with proper exception handling and temporary file cleanup on errors.

### 🇷🇺 Известные ограничения | 🇬🇧 Known Limitations
- ⚠️ **Code signing не реализован**: Windows SmartScreen будет показывать warning при запуске инсталлятора. Требуется покупка Windows code signing certificate (отложено до появления бюджета).
- ⚠️ **Code signing not implemented**: Windows SmartScreen will show warning when running installer. Windows code signing certificate purchase required (deferred until budget available).

- 📌 **Auto-install требует подписи**: Для полноценного auto-install без SmartScreen требуется цифровая подпись инсталлятора.
- 📌 **Auto-install requires signing**: Digital signature is required for full auto-install without SmartScreen.

## [1.6.1] - 2026-05-15

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Ротация профилей браузера**: для кнопок с ротацией теперь можно выбрать конкретные профили, участвующие в цикле.
- **Browser profile rotation**: Buttons with rotation now support selecting specific profiles to cycle through.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Окно настройки кнопки**: восстановлена компактная компоновка настроек без регрессии визуального дизайна.
- **Button settings window**: Restored compact settings layout without visual design regression.

- **Метаданные сборки**: поле компании в `AiteBar.exe` синхронизировано с документацией, About-окном и инсталлятором (`Codebdbd`).
- **Build metadata**: Company field in `AiteBar.exe` synchronized with documentation, About window and installer (`Codebdbd`).

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Встроенные быстрые инструменты**: ошибки запуска скриншота, записи видео, калькулятора, проводника, загрузок и поиска теперь логируются и показываются пользователю без падения приложения.
- **Built-in quick tools**: Errors in screenshot, video recording, calculator, file explorer, downloads and search now logged and shown to user without application crash.

- **Релизная сборка**: пересобран publish и installer для версии `1.6.1` с актуальными метаданными.
- **Release build**: Rebuilt publish and installer for version `1.6.1` with current metadata.

## [1.4.0] - 2026-04-18

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Явные типы действий**: для ссылок, программ, файлов, папок и скриптов теперь используются отдельные action type без неоднозначной трактовки legacy `Exe`.
- **Explicit action types**: Links, programs, files, folders and scripts now use separate action types without ambiguous legacy `Exe` interpretation.

- **Регрессионные тесты релизной синхронизации**: добавлены проверки, что версии проекта, сборки и инсталлятора не расходятся между собой.
- **Release sync regression tests**: Added checks to ensure project, build and installer versions don't diverge.

- **Перетаскивание рабочей панели**: положение панели теперь можно менять прямо на экране через drag-and-drop за специальное ушко, с сохранением нового края и монитора после отпускания мыши.
- **Panel drag-and-drop**: Panel position can now be changed on screen via drag-and-drop with a handle, preserving new edge and monitor after mouse release.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Уточнена логика drag-and-drop**: переупорядочивание элементов стало строже и предсказуемее для релизной ветки.
- **Refined drag-and-drop logic**: Element reordering became stricter and more predictable for release branch.

- **Сборка инсталлятора**: версия setup теперь берется из `AiteBar.csproj`, чтобы не расходиться с версией приложения.
- **Installer build**: Setup version now taken from `AiteBar.csproj` to stay in sync with application version.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- Устранен риск выпуска инсталлятора с устаревшей версией относительно `AiteBar.exe`.
- Eliminated risk of releasing installer with outdated version relative to `AiteBar.exe`.

- Убрана зависимость релизной маркировки от legacy-обработки исполняемых файлов.
- Removed dependency of release tagging on legacy executable file handling.

## [1.3.0] - 2026-04-14

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Универсальная поддержка браузеров**: Теперь поддерживаются Chrome, Edge, Brave, Yandex, Opera, Opera GX, Vivaldi и **Firefox**.
- **Universal browser support**: Now supports Chrome, Edge, Brave, Yandex, Opera, Opera GX, Vivaldi and **Firefox**.

- **Менеджер профилей Firefox**: Реализован автоматический поиск и выбор профилей Firefox (через парсинг `profiles.ini`).
- **Firefox profile manager**: Implemented automatic Firefox profile discovery and selection (via `profiles.ini` parsing).

- **Автоматическое извлечение иконок**: При добавлении программ (.exe) или ярлыков (.lnk) приложение автоматически извлекает их иконку и сохраняет в AppData.
- **Automatic icon extraction**: Application automatically extracts and saves icons from programs (.exe) or shortcuts (.lnk) to AppData.

- **Проект юнит-тестирования**: Добавлен проект `AiteBar.Tests` на базе xUnit для проверки критически важной логики.
- **Unit testing project**: Added `AiteBar.Tests` project based on xUnit for testing critical logic.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Рестайлинг окна настроек**:
    - Внедрена вкладчатая структура (Tabs) для удобного разделения параметров.
    - Полностью переработана цветовая схема (снижен контраст, профессиональные темные тона).
    - Удалена вертикальная прокрутка за счет оптимизированной компоновки.
- **Settings window redesign**:
    - Implemented tabbed structure (Tabs) for convenient parameter separation.
    - Completely redesigned color scheme (reduced contrast, professional dark tones).
    - Removed vertical scrolling through optimized layout.

- **Архитектурный рефакторинг**:
    - Весь низкоуровневый код WinAPI вынесен в отдельный класс `NativeMethods`.
    - Логика путей и работы с браузерами централизована в `PathHelper` и `BrowserHelper`.
    - Код обновлен до современных стандартов **C# 12** (Collection Expressions).
- **Architectural refactoring**:
    - All low-level WinAPI code extracted to separate `NativeMethods` class.
    - Path and browser logic centralized in `PathHelper` and `BrowserHelper`.
    - Code updated to modern **C# 12** standards (Collection Expressions).

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- Многочисленные предупреждения статического анализатора (CA1416, CA1822, IDE0057 и др.).
- Numerous static analyzer warnings (CA1416, CA1822, IDE0057 and others).

- Проблема с рассинхронизацией путей к логам и настройкам в разных частях приложения.
- Fixed path desynchronization issue between logs and settings in different parts of application.

## [1.0.0] - 2026-03-24

### 🇷🇺 Добавлено | 🇬🇧 Added
- Выпуск первой официальной релизной версии AiteBar (v1.0.0) (ранее SmartScreenDock).
- Release of first official version of AiteBar (v1.0.0) (formerly SmartScreenDock).

- Кроссплатформенный менеджер иконок и встроенные шрифты.
- Cross-platform icon manager and embedded fonts.

- Поддержка Material Symbols и Fluent System Icons (не требуют установки на ОС).
- Support for Material Symbols and Fluent System Icons (no OS installation required).

- Выбор и поиск брендовых иконок Font Awesome Brands.
- Selection and search of Font Awesome Brands icons.

- Обновленный интерфейс окна настроек: улучшенное центрированное превью иконок.
- Updated settings window interface: improved centered icon preview.

- Быстрая конфигурация цветов из палитры и точечный ввод HEX.
- Quick color configuration from palette and HEX input.

- Ротация профилей Chrome для последовательного использования разных аккаунтов.
- Chrome profile rotation for sequential use of different accounts.

- Запуск веб-сайтов в режиме "Инкогнито" и "App Mode".
- Launch websites in "Incognito" and "App Mode".

- Поддержка запуска консольных команд с диалогом подтверждения.
- Support for running console commands with confirmation dialog.

- Автоматическая ротация логов (создание файлов .bak) при превышении 1МБ.
- Automatic log rotation (creating .bak files) when exceeding 1MB.

### 🇷🇺 Изменено | 🇬🇧 Changed
- Динамически вычисляемая ширина левого/правого блока настроек для адаптивной компоновки UI.
- Dynamically calculated width of left/right settings block for adaptive UI layout.

- Отказ от системного шрифта Segoe Fluent Icons для лучшей совместимости на Windows 10.
- Removed Segoe Fluent Icons system font for better Windows 10 compatibility.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- Проблема с отображением символов FontAwesome Brands в каталоге.
- Fixed FontAwesome Brands symbols display issue in catalog.

- Утечка дескрипторов (не вызывался `Dispose()`) при запуске процессов `Process.Start` в `MainWindow.xaml.cs`.
- Fixed file handle leak (missing `Dispose()` call) in `Process.Start` calls in `MainWindow.xaml.cs`.
