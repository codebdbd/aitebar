# 1. Обзор проекта

AiteBar — это скрываемая edge-панель быстрого доступа для Windows. Она появляется при наведении курсора на край экрана и предоставляет быстрый доступ к пользовательским действиям, встроенным утилитам, контекстам и системной интеграцией через tray/hotkeys.

## Назначение продукта
Обеспечить быстрый и удобный доступ к часто используемым ресурсам: сайтам, программам, файлам, папкам, скриптам и системным командам.

## Основные задачи
- Показ/скрытие панели при наведении на край экрана или по горячим клавишам
- Управление пользовательскими кнопками (добавление, редактирование, удаление, дублирование, переименование, перетаскивание)
- Поддержка нескольких контекстов (панелей) кнопок (фиксировано 8 контекстов)
- Выполнение различных типов действий (веб, программы, файлы, папки, скрипты, команды, горячие клавиши)
- Импорт и экспорт панелей в ZIP-архивах .aitebarpanel
- Системная интеграция через tray icon и глобальные горячие клавиши

## Основные сценарии использования
- Быстрый запуск сайтов в выбранном браузере и профиле
- Открытие программ, файлов и папок
- Выполнение скриптов (.bat, .cmd, .ps1, .py)
- Использование встроенных утилит (поиск, скриншот, запись видео, калькулятор, проводник, таймер, цветовой пикер, Quick Note, Дзен-редактор, File Sorter, Icon Converter, QR Code Generator, Clipboard Manager, Show Desktop, Apps Folder, Copilot)
- Переключение между контекстами кнопок
- Импорт/экспорт панелей с кнопками

## Ключевые возможности
- Скрытая панель, появляющаяся при наведении
- Поддержка 4 сторон экрана (Top, Bottom, Left, Right)
- Поддержка нескольких мониторов
- Глобальные горячие клавиши
- 8 контекстов (панелей) кнопок (первый включен по умолчанию)
- Импорт/экспорт панелей в ZIP-архивах .aitebarpanel (включая пользовательские иконки)
- Ротация профилей браузера
- Поддержка различных браузеров (Chrome, Edge, Brave, Yandex, Opera, OperaGX, Vivaldi, Firefox)
- Встроенная библиотека иконок (Material Symbols, Fluent System Icons, Font Awesome Brands)
- Быстрые заметки с поддержкой Markdown
- Локализация (ru, en, de, uk)

---

# 2. Архитектура системы

## Высокоуровневая схема компонентов

```
┌─────────────────────────────────────────────────────────────────┐
│                         Windows UI Layer                          │
│  ┌──────────────┐  ┌─────────────────┐  ┌──────────────────┐  │
│  │  MainWindow  │  │  SettingsWindow │  │  AppSettingsWindow│  │
│  └──────┬───────┘  └────────┬────────┘  └────────┬─────────┘  │
│  ┌──────────────┐  ┌─────────────────┐  ┌──────────────────┐  │
│  │QuickNoteWindow│  │IconPickerWindow │  │Other Windows...  │  │
│  └──────────────┘  └─────────────────┘  └──────────────────┘  │
└─────────┼─────────────────────┼──────────────────────┼────────────┘
          │                     │                      │
          └─────────────────────┼──────────────────────┘
                                │
┌───────────────────────────────┼───────────────────────────────────┐
│                         Services Layer                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌─────────────────┐ │
│  │ ActionService    │  │AppSettingsService │  │PanelPackageService│ │
│  └──────────────────┘  └──────────────────┘  └─────────────────┘ │
│  ┌──────────────────┐  ┌──────────────────┐  ┌─────────────────┐ │
│  │QuickNoteService  │  │NativeIntegrationSvc│  │ LocalizationSvc │ │
│  └──────────────────┘  └──────────────────┘  └─────────────────┘ │
└───────────────────────┬───────────────────────────────────────────┘
                        │
┌───────────────────────┼───────────────────────────────────────────┐
│                      Helpers Layer                                  │
│  ┌──────────┐ ┌────────────┐ ┌──────────────┐ ┌───────────────┐ │
│  │BrowserHel│ │PathHelper  │ │PanelLayoutHel│ │ContextStateHel │ │
│  └──────────┘ └────────────┘ └──────────────┘ └───────────────┘ │
│  ┌──────────┐ ┌────────────┐ ┌──────────────┐ ┌───────────────┐ │
│  │FontHelper│ │IconHelper  │ │ProfileRotation│ │ActionTargetHel│ │
│  └──────────┘ └────────────┘ └──────────────┘ └───────────────┘ │
│  ┌──────────────────┐ ┌──────────────────────┐                     │
│  │QuickNoteMarkdown │ │PanelPackageMapper    │                     │
│  └──────────────────┘ └──────────────────────┘                     │
└───────────────────────┬───────────────────────────────────────────┘
                        │
┌───────────────────────┼───────────────────────────────────────────┐
│                   Native Integration Layer                           │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │                     NativeMethods.cs                           │ │
│  │  - Win32 interop (hotkeys, mouse hooks, window positioning) │ │
│  └──────────────────────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────────────┘
```

## Описание каждого уровня системы

### Windows UI Layer
Слой пользовательского интерфейса на базе WPF. Основные окна и компоненты:
- `MainWindow` — основная панель с кнопками
- `TaskbarPositionIndicatorWindow` — индикатор положения панели на панели задач
- `SettingsWindow` — редактирование отдельной кнопки
- `AppSettingsWindow` — общие настройки приложения и раздел «О программе»
- `QuickNoteWindow` — быстрые заметки с Markdown
- `TimerStopwatchWindow` — таймер и секундомер
- `ScreenColorPickerWindow` — цветовой пикер
- `FileSorterWindow` — окно утилиты сортировки файлов
- `IconConverterWindow` — окно утилиты конвертации иконок
- `QRCodeGeneratorWindow` — окно генератора QR-кодов
- `ClipboardManagerWindow` — окно менеджера буфера обмена
- `TextProcessingWindow` — окно AI-обработки текста (дифф оригинала/результата, Undo/Redo, отмена потока)
- `ZenEditorWindow` — полноэкранный Дзен-редактор с темами, множеством документов, поиском по документам и экспортом TXT
- `IconPickerWindow` — выбор иконки
- `AiConnectionDialog` — диалог создания/редактирования одного AI-подключения с проверкой связи по API
- `ColorPickerDialog` — диалог выбора цвета для кнопок (палитра, HEX-поле, привязка к теме)
- `QuickNoteLinkDialog` — модальный диалог вставки/редактирования гиперссылки в Quick Note (поля URL, отображаемый текст, подтверждение/отмена)
- `ZenEditorDocumentPicker` — отдельное окно-пикер документов Дзен-редактора: список внутренних документов, создание, переименование, удаление в корзину и восстановление недавно удалённых
- `ZenParagraphEditor` — кастомный WPF-контрол редактирования одного параграфа в Дзен-редакторе с вводом/удалением и визуальной симметрией
- `DarkDialog` — диалоговое окно с подтверждением
- `TextPromptDialog` — модальное диалоговое окно однострочного или многострочного ввода текста (используется при переименовании документа, создании папки и т. д.)
- `HotkeyCaptureBox` — кастомный элемент для захвата комбинации hotkey в UI настроек (интегрируется с `HotkeyCaptureHelper` и валидацией)
- `ComboBoxPopupChrome` — кастомный WPF-хром для выпадающих списков ComboBox, обеспечивающий единый внешний вид pop-up в тёмной теме
- `DarkWindow` — базовый класс окна в тёмной теме с скрытым системным chrome,CornerRadius, симметричными отступами; на нём строятся QuickNote, ZenEditor, TextProcessing и другие окна утилит
- `RotationProfileSelectionWindow` — выбор профилей для ротации
- `OverflowWrapPanel` — кастомная WPF панель для многострочного (multi-band) расположения кнопок

### Services Layer
Сервисы, реализующие бизнес-логику:
- `ActionService` — выполнение пользовательских действий и системных утилит
- `AppSettingsService` — загрузка, сохранение и нормализация настроек
- `TaskbarPositionIndicatorService` — управление индикатором положения панели (показ, drag-and-drop, координаты)
- `PanelPackageService` — импорт и экспорт панелей (ZIP-архивирование)
- `QuickNoteService` — управление быстрыми заметками
- `TextProcessingService` — AI-обработка текста (потоковые ответы SSE, защита технических фрагментов, отмена, Undo/Redo, дифф)
- `ZenEditorStore` — локальное хранилище документов Дзен-редактора с автосохранением, восстановлением и корзиной недавно удалённых
- `AiGateway` — общий шлюз HTTP-запросов ко всем настроенным AI-провайдерам; роутинг по моделям, ротация ключей одного провайдера без подмены модели
- `AiCredentialStore` — хранение и считывание API-ключей в Windows Credential Manager (префикс `AiteBar/AI/<connection-id>`)
- `AiProviderCatalog` — каталог поддерживаемых AI-провайдеров (OpenRouter, Cerebras, Gemini, Groq, GitHub Models, Mistral) и контрактов их API
- `AiSettingsNormalizer` — нормализация и валидация настроек AI-подключений (имя, BaseUrl, ModelMode, DefaultModel, UseAutoModel и т. д.). Реализован как `internal static class AiSettingsNormalizer` внутри файла `AiProviderCatalog.cs`, не в отдельном файле.
- `UpdateCheckService` — проверка обновлений приложения
- `UpdateCheckUi` — UI-состояния, модальные подтверждения и сообщения для сценария проверки обновлений; координирует вызов `UpdateCheckService`, показ прогресса, выбор «скачать сейчас / позже» и валидацию URL release page
- `AppContextMenuFactory` — единая фабрика геометрии, глифов, отступов и вертикального выравнивания для всех программно создаваемых меню (панель, tray, индикатор, Дзен-редактор, Обработка текста); гарантирует симметричность ContextMenu и одинаковый pixel-perfect вид
- `NativeIntegrationService` — низкоуровневая интеграция с Windows (mouse hooks)
- `LocalizationService` — локализация интерфейса (ru, en, de, uk)
- `UnifiedButtonService` — построение списка унифицированных кнопок (пользовательские + системные)
- `FileSorterService` — сервис для работы FileSorter утилиты
- `IconConverterService` — сервис для работы IconConverter утилиты
- `QRCodeService` — генерация QR-кодов и экспорт PNG/SVG
- `ClipboardHistoryService` — runtime-история текста и изображений из буфера обмена
- `HotkeyService` — регистрация и управление глобальными горячими клавишами
- `TelemetryService` — телеметрия (Sentry)
- `Logger` — логирование ошибок и событий
- `UiDispatcher` — централизованная обёртка над `Application.Current.Dispatcher`; все фоновые сервисы (`TextProcessingService`, `ZenEditorStore`, `HotkeyService`, `UpdateCheckService`) переключаются на UI-поток строго через `UiDispatcher`, чтобы не вызывать Dispatcher напрямую и не допускать race на коллекциях
- `KeyboardFocusVisualService` — единое управление логическим фокусом и фокус-обводкой в окнах утилит: гарантирует, что Enter/Esc/стрелки фокусируются на ожидаемых элементах, и фокус-рамка всегда пиксель-перфект
- `IndicatorPositionHelper` / `PanelPositionHelper` / `TaskbarGeometryHelper` / `EasingHelper` (расчётная часть) — см. Helpers Layer
- `HotkeyCaptureHelper` / `HotkeyKeyCatalog` / `HotkeyValidationHelper` — единый стек захвата, каталога и валидации hotkey в UI настроек
- `AppSettingsDiscreteChoiceHelper` / `AppSettingsSelectionHelper` / `AppSettingsSectionNavigationHelper` — UI-хелперы формы общих настроек
- `SettingsWindowValidationHelper` — валидация формы редактирования одной кнопки: пустой URL/путь, запрещённые символы, длины полей, проверка существования файла/папки, сообщения об ошибках на локализованном языке

### Helpers Layer
Статические классы-помощники для различных задач:
- `BrowserHelper` — работа с браузерами (поиск exe, получение профилей)
- `PathHelper` — пути к файлам конфигурации и данных
- `PanelLayoutHelper` — расчет геометрии панели
- `PanelPositionHelper` — расчет координат панели и ближайшего края при drag handle
- `TaskbarGeometryHelper` — расчет положения индикатора на панели задач
- `ContextStateHelper` — работа с контекстами (8 фиксированных контекстов)
- `FontHelper`, `IconHelper` — работа с иконками и шрифтами
- `ProfileRotationHelper` — ротация профилей браузера
- `ActionTargetHelper` — валидация целей действий
- `QuickNoteContracts` — контракты (интерфейсы, типы) и POCO-модели Quick Note, разделяемые между окном, сервисом и persistence-слоем
- `QuickNotePersistence` — чтение/запись файла быстрой заметки на диск с round-trip сохранением Markdown, обработкой conflict-copy и резервными копиями
- `QuickNoteMarkdown` — парсинг и генерация Markdown для быстрых заметок
- `PanelPackageMapper` — маппинг между CustomElement и PanelPackageElement
- `ActivationZoneHelper` — проверка, находится ли курсор в зоне активации панели
- `UtilityWindowLayoutHelper` — общий расчёт геометрии окон утилит (QuickNote, TimerStopwatch, TextProcessing, QRCodeGenerator, ClipboardManager и т. д.): clamp в рабочую область монитора, чтение/запись `Left/Top/Width/Height` из настроек, fallback-значения при первом открытии
- `QuickNoteLayoutHelper` — расчет геометрии окна Quick Note
- `QuickNoteDocumentHelper` — работа с документом Quick Note
- `QuickNoteTheme` — темы для Quick Note
- `TimerStopwatchLayoutHelper` — расчет геометрии окна таймера/секундомера
- `TimerStopwatchFormatter` — форматирование времени для таймера/секундомера
- `IcoEncoder` — кодирование изображений в ICO формат
- `IconConverterModels` — модели данных для конвертации иконок
- `EasingHelper` — вспомогательные функции для анимаций
- `Constants` — константы приложения
- `HotkeyValidationHelper` — валидация горячих клавиш
- `HotkeyKeyCatalog` — каталог доступных клавиш для горячих клавиш
- `HotkeyCaptureHelper` — обработчик захвата комбинации клавиш в UI (используется `HotkeyCaptureBox`-контролом)
- `PanelPackageManifest` — модель манифеста пакета панелей
- `UtilityButtonCatalog` — централизованный каталог всех 18 встроенных кнопок утилит: Id, локализация, глифы, видимость по `ShowPreset*`, порядок по умолчанию. Единый источник правды для MainWindow, AppSettingsWindow и импорта/экспорта.
- `ZenEditorAsyncCommandGuard` — общая граница обработки ошибок асинхронных команд Дзен-редактора; перехватывает исключение и передаёт его UI восстановления
- `ZenEditorShortcutResolver` — чистый резолвер сочетаний поиска и тем Дзен-редактора: `Ctrl+F`, `F3`, `Shift+F3`, `Ctrl+Alt+↑/↓` и `Ctrl+Alt+1..5`
- `ZenEditorUndoHistory` — ограниченная история Undo/Redo текстовых изменений и диапазонов форматирования; поддерживает группировку последовательного ввода и `Ctrl+Z`/`Ctrl+Y`
- `ZenEditorSearchHelper` — буквальный регистронезависимый поиск внутри текущего документа с переходом вперёд/назад и циклическим продолжением
- `ZenEditorTextHelper` — текстовые утилиты Дзен-редактора: заголовок документа, нормализация TXT-экспорта, расчёт одного изменения и инкрементальное обновление диапазонов форматирования
- `ZenEditorThemeCatalog` — каталог тем Дзен-редактора (тёмные/светлые, шрифт, межстрочное расстояние, отступы параграфов); применяется через ресурсы окна
- `ZenEditorModels` — модели Дзен-редактора: `ZenEditorDocument`, `ZenEditorTextStyle`, индекс хранилища, метаданные, summaries, результат загрузки и описание темы
- `AiProviderClient` — типизированный HTTP-клиент одного настроенного AI-подключения: оборачивает `HttpClient`, добавляет заголовки авторизации, SSE-парсинг, retry и передачу трассировки в `AiGateway`
- `AiModels` — POCO-модели и enum AI-подсистемы (AiProviderId, AiConnection, AiModelInfo, AiModelPricing, AiRequestOptions, AiChatMessage и т. д.); используются `AiProviderCatalog`, `AiGateway`, `TextProcessingService`, `AiConnectionDialog`
- `QRCodeShortcutHelper` — горячие клавиши и input bindings окна генератора QR-кодов: Ctrl+C скопировать PNG, Ctrl+S сохранить, Ctrl+E экспорт SVG, очистка поля по Escape
- `ClipboardTextTransforms` — преобразования текста в Clipboard Manager: копирование «как одна строка», обрезка пробелов, нормализация переносов строк, URL-encode для промптов и команд; используются как контекстные действия над клипом
- `UnifiedButton` — DTO/контракт между `UnifiedButtonService` и UI-панелью; одна унифицированная кнопка представляет либо пользовательский `CustomElement`, либо одну из 18 встроенных утилит `UtilityButtonDefinition` из каталога.
- `ActionExecutionResult` — enum-результат выполнения `ActionService.ExecuteAsync` (Success / FailedWithWarning / FailedCancelled / Failed) с сообщением об ошибке; используется UI feedback и тестами.
- `QRCodeModels` — POCO и enum для QR Code Generator (уровень коррекции ошибок, режим экспорта PNG/SVG, размеры QrCodeData); коррелирует по назначению с `IconConverterModels` и `ZenEditorModels`.

### MainWindow: структура partial-классов

`MainWindow` в `MainWindow.xaml.cs` является partial-классом. В соответствии с практикой из [AGENTS.md](../AGENTS.md) («часть обработчиков вынесена в partial-файлы»), базовый файл содержит ядро UI (конструктор, ресурсы, привязки данных и общие поля), а крупные обработчики сгруппированы по теме в отдельных `MainWindow.*.cs`:

| Файл | Ответственность |
|---|---|
| `MainWindow.xaml.cs` | Базовый файл: конструктор, `Loaded/Closing`, свойства панели (анимации, геометрия), построение кнопок из `UnifiedButtonService`, показ/скрытие, привязка контекстов |
| `MainWindow.TrayMenuHandler.cs` | Построение и обработка tray-меню (выход, окно настроек, проверка обновлений, контексты, включение/отключение ShowPreset*). |
| `MainWindow.KeyboardNavigationHandler.cs` | Глобальные hotkey навигации по панели (вправо/влево/вверх/вниз, Enter/Space/Escape), колесо мыши для переключения контекстов, взаимодействие с `ContextWheelSwitchCooldownMs` |
| `MainWindow.PanelDragHandler.cs` | Drag-and-drop самой панели (drag handle): перетаскивание на другой край/монитор, рассчёт ближайшего края через `PanelPositionHelper`, сохранение `Edge` и `MonitorIndex` |
| `MainWindow.DragAndDropHandler.cs` | Drag-and-drop **внутри панели** — перестановка пользовательских кнопок и встроенных утилит: начало drag, ховер над ячейкой, drop, commit порядка, анимации `AnimationFadeMs`/`AnimationSlideMs` |
| `MainWindow.DropHandler.cs` | Drop извне (файлы, URL, текст из проводника/браузера) на панель; создание новых кнопок из drop-полезной нагрузки и confirmation через `DarkDialog` |
| `MainWindow.ImportExportHandler.cs` | Пункты меню «Экспортировать панель»/«Импортировать панель»: вызов `PanelPackageService`, выбор `.aitebarpanel` ZIP-файла, подтверждение перезаписи, импорт/экспорт иконок |

Все partial-файлы находятся в одном namespace `AiteBar` и дополняют один класс `MainWindow`; общие поля (например, `_overlay`, `_panelState`, `_dragAndDrop`, координаты) живут в базовом `MainWindow.xaml.cs`.

### Subsystem DTO / POCO Models

Для крупных подсистем контракты и data-only типы вынесены в отдельные файлы-модели. Они не содержат бизнес-логики и сериализуются в JSON (настройки, манифест `.aitebarpanel`, окна ViewModel, ответы AI API):

| Файл-модель | Назначение |
|---|---|
| [Models.cs](../AiteBar/Models.cs) | Корневые типы настроек и UI: `PanelContext`, `HotkeyBinding`, enums `DockEdge`, `BrowserType`, `ActionType`, `CustomElement` (пользовательская кнопка), `AppSettings`, `SentrySettings`, `AiSettings`, `FileSortUndoState`, `AiModelPricing`. |
| [AiModels.cs](../AiteBar/AiModels.cs) | AI-контракты: `AiProviderId` (enum из 6 провайдеров), `AiConnection`, `AiModelInfo`, `AiRequestOptions`, `AiChatMessage`, ответы роутинга. Используются `AiProviderCatalog`, `AiGateway`, `TextProcessingService`, `AiConnectionDialog`. |
| [ZenEditorModels.cs](../AiteBar/ZenEditorModels.cs) | Документ, диапазоны форматирования, индекс хранилища, метаданные, summaries, результат загрузки и описание темы Дзен-редактора. |
| [IconConverterModels.cs](../AiteBar/IconConverterModels.cs) | Icon Converter DTO (входные форматы, выходные размеры, профиль ICO). |
| [QRCodeModels.cs](../AiteBar/QRCodeModels.cs) | QR Code Generator DTO (ErrorCorrectionLevel, режим экспорта PNG/SVG, размеры). |
| [UnifiedButton.cs](../AiteBar/UnifiedButton.cs) | Унифицированный DTO одной кнопки для UI-панели; представляет либо `CustomElement`, либо `UtilityButtonDefinition`. |
| [ActionExecutionResult.cs](../AiteBar/ActionExecutionResult.cs) | Result-тип возврата `ActionService.ExecuteAsync`: Success/FailedWithWarning/FailedCancelled/Failed + сообщение; используется UI feedback и unit-тесты. |
| [PanelPackageManifest.cs](../AiteBar/PanelPackageManifest.cs) | JSON-схема файла `manifest.json` внутри `.aitebarpanel` ZIP (AppVersion, ExportedAt, Elements, Images). |
| [QuickNoteContracts.cs](../AiteBar/QuickNoteContracts.cs) | Интерфейсы и shared POCO для QuickNote (используются окном, `QuickNoteService` и `QuickNotePersistence`). |
| [TextProcessingUiState.cs](../AiteBar/TextProcessingUiState.cs) + [TextProcessingMode.cs](../AiteBar/TextProcessingMode.cs) | Состояния UI и четыре preset-режима Обработки текста: Proofread, Typography, Cleanup и LiteraryEdit. |

### Native Integration Layer
Низкоуровневая интеграция с Windows API через `NativeMethods.cs`, содержащий P/Invoke объявления для Win32 функций:
- Регистрация глобальных горячих клавиш
- Low-level mouse hook
- Позиционирование окон
- Отправка ввода (SendInput)

## Описание взаимодействия уровней
UI Layer использует Services Layer для выполнения бизнес-логики. Services Layer использует Helpers Layer для вспомогательных вычислений и Native Integration Layer для взаимодействия с операционной системой.

---

# 3. Используемые технологии

| Технология | Назначение | Где используется |
|------------|------------|-------------------|
| .NET 10 | Платформа приложения | Весь проект |
| WPF | Пользовательский интерфейс | Все окна и элементы UI |
| Windows Forms | NotifyIcon (трей) | MainWindow.xaml.cs |
| System.Text.Json | Сериализация/десериализация настроек | AppSettingsService, PanelPackageService |
| System.IO.Compression | ZIP-архивирование панелей | PanelPackageService |
| Win32 API (P/Invoke) | Глобальные горячие клавиши, mouse hooks, позиционирование окон | NativeMethods.cs, NativeIntegrationService |

---

# 4. Структура проекта

## AiteBar/
Основной проект приложения.

**Назначение:** Содержит весь код приложения, включая UI, сервисы, модели и вспомогательные классы.

**Ключевые файлы:**
- Models.cs — модели данных (AppSettings, CustomElement, PanelContext, и т.д.)
- UnifiedButton.cs — модель унифицированной кнопки (для пользовательских и системных кнопок)
- MainWindow.xaml / MainWindow.xaml.cs — основное окно приложения
- OverflowWrapPanel.cs — кастомная WPF панель для многострочного (multi-band) расположения кнопок
- AppSettingsService.cs — управление настройками
- ActionService.cs — выполнение действий
- PanelPackageService.cs — импорт/экспорт панелей
- QuickNoteService.cs — управление быстрыми заметками
- UpdateCheckService.cs — проверка обновлений
- UpdateCheckUi.cs — UI хелпер для проверки обновлений
- NativeMethods.cs — Win32 interop
- PathHelper.cs — пути к данным
- Logger.cs — логирование ошибок
- LocalizationService.cs — локализация

**Связи с другими частями системы:** Является основным исполняемым проектом, ссылается на сам себя и использует все внутренние компоненты.

## AiteBar.Tests/
Тестовый проект.

**Назначение:** Содержит unit-тесты для ключевых компонентов приложения.

**Ключевые файлы:**
- ActionServiceTests.cs
- AppSettingsServiceTests.cs
- PanelLayoutHelperTests.cs
- PanelPackageServiceTests.cs
- и другие тесты для helper-классов

**Связи с другими частями системы:** Ссылается на AiteBar для тестирования его компонентов.

## docs/
Документация проекта.

**Назначение:** Содержит внутреннюю и внешнюю документацию.

## installer/
Скрипты для создания инсталлятора.

**Назначение:** Содержит Inno Setup скрипт и PowerShell скрипт для сборки инсталлятора.

**Ключевые файлы:**
- AiteBar.iss — Inno Setup скрипт
- Build-Installer.ps1 — PowerShell скрипт для сборки инсталлятора

---

# 5. Архитектурные модули

## MainWindow
**Назначение:** Основное окно приложения, содержащее панель с кнопками.

**Ответственность:**
- Показ и скрытие панели
- Отображение и управление кнопками
- Обработка drag-and-drop для переупорядочивания кнопок и смены стороны панели
- Управление контекстами
- Управление tray icon
- Регистрация глобальных горячих клавиш
- Обработка mouse hook для скрытия панели при клике вне

**Входящие зависимости:**
- AppSettingsService
- ActionService
- PanelPackageService
- NativeIntegrationService

**Исходящие зависимости:**
- SettingsWindow
- AppSettingsWindow
- QuickNoteWindow
- IconPickerWindow
- и другие вспомогательные окна

**Основные компоненты:**
- RootBorder — корневой контейнер панели
- MainPanel — основная панель
- FixedPanel — панель с системными кнопками
- UnifiedButtonsPanel (OverflowWrapPanel) — панель с пользовательскими кнопками (кастомная панель для многострочного расположения)
- ControlBlock — блок с кнопкой добавления
- AppSettingsBlock — блок с кнопкой настроек

**Основные классы:**
- MainWindow (partial class)

**Основные функции:**
- ShowDock() / HideDock() / ToggleDock()
- RefreshPanel()
- RegisterGlobalHotkey()
- InitTrayIcon()
- ImportIntoCurrentPanelAsync() / ExportCurrentPanelAsync()

**Потоки данных:**
- Настройки загружаются из AppSettingsService
- Кнопки отображаются на основе _elements из AppSettingsService
- Действия выполняются через ActionService

## AppSettingsService
**Назначение:** Управление настройками приложения и пользовательскими кнопками.

**Ответственность:**
- Загрузка настроек из файла (settings.json)
- Миграция из старого формата (custom_buttons.json)
- Сохранение настроек в файл
- Нормализация состояния приложения
- Управление списком пользовательских кнопок
- Управление контекстами (8 фиксированных)

**Входящие зависимости:**
- PathHelper
- ContextStateHelper
- LocalizationService

**Исходящие зависимости:**
- Нет

**Основные компоненты:**
- _appSettings — текущие настройки
- _elements — список пользовательских кнопок
- _saveSemaphore — семафор для потокобезопасного сохранения
- WriteSettingsWithBackupAsync() — запись `settings.json` через временный файл и замену текущего файла с сохранением предыдущей версии в `settings.json.backup.0`
- RotateBackups() / RotateExistingBackupsOnly() — ротация резервных копий настроек

**Основные классы:**
- AppSettingsService

**Основные функции:**
- LoadAsync()
- SaveAsync()
- WriteSettingsWithBackupAsync()
- NormalizeAppState()
- SaveElementAsync()
- ReorderElements()
- CloneElement()

**Потоки данных:**
- Загружает данные из SettingsFile (settings.json)
- При сохранении сериализует AppSettings в JSON, записывает данные во временный файл рядом с SettingsFile, затем заменяет SettingsFile через файловую операцию replace/move
- Если SettingsFile уже существовал, предыдущая версия сохраняется как `settings.json.backup.0`, а старые бэкапы сдвигаются до лимита MaxBackupCount

## ActionService
**Назначение:** Выполнение пользовательских действий и системных утилит.

**Ответственность:**
- Выполнение веб-действий (открытие URL в браузере)
- Выполнение действий с программами, файлами, папками
- Выполнение скриптов (.bat, .cmd, .ps1, .py)
- Выполнение команд
- Выполнение горячих клавиш
- Запуск системных утилит (поиск, скриншот, калькулятор, и т.д.)
- Запуск встроенных утилит через UtilityRegistry

**Входящие зависимости:**
- AppSettingsService
- BrowserHelper
- ProfileRotationHelper
- NativeMethods
- UtilityRegistry

**Исходящие зависимости:**
- QuickNoteWindow
- ScreenColorPickerWindow
- TimerStopwatchWindow
- FileSorterWindow

**Основные компоненты:**
- Использует UtilityRegistry для получения и запуска утилит (не хранит экземпляры окон напрямую)

**Основные классы:**
- ActionService
- UtilityRegistry
- IUtility (интерфейс)

**Основные функции:**
- ExecuteCustomActionAsync()
- ExecuteWebActionAsync()
- StartSearchAsync()
- StartScreenshotAsync()
- LaunchUtilityAsync() (новый универсальный метод для запуска утилит)

**Потоки данных:**
- Принимает CustomElement для выполнения действия
- Использует BrowserHelper для работы с браузерами
- Использует NativeMethods для отправки горячих клавиш
- Для запуска утилит использует UtilityRegistry, содержащий все зарегистрированные утилиты

## Utility System
**Назначение:** Универсальная система для регистрации и запуска встроенных утилит.

**Ответственность:**
- Регистрация всех утилит в центральном реестре
- Универсальный запуск утилит по уникальному идентификатору
- Простое добавление новых утилит без изменения основного кода

**Входящие зависимости:**
- Нет

**Исходящие зависимости:**
- ActionService
- Окна утилит (QuickNoteWindow, TimerStopwatchWindow, и т.д.)

**Основные компоненты:**
- IUtility — интерфейс для всех утилит
- UtilityRegistry — статический класс для регистрации и получения утилит
- Классы утилит: QuickNoteUtility, TimerStopwatchUtility, ColorPickerUtility, FileSorterUtility, IconConverterUtility, QRCodeGeneratorUtility, ClipboardManagerUtility, TextProcessingUtility, ZenEditorUtility

**Основные функции:**
- UtilityRegistry.Register() — регистрация утилиты
- UtilityRegistry.GetById() — получение утилиты по ID
- IUtility.Launch() — запуск утилиты

**Потоки данных:**
- При запуске утилиты ActionService вызывает UtilityRegistry.GetById() для получения экземпляра
- Затем вызывает IUtility.Launch() для запуска окна утилиты

## PanelPackageService
**Назначение:** Импорт и экспорт панелей кнопок в ZIP-архивах .aitebarpanel.

**Ответственность:**
- Экспорт текущей панели в ZIP-архив .aitebarpanel (включая manifest.json и пользовательские иконки)
- Чтение превью импорта
- Импорт панели из ZIP-архива .aitebarpanel в текущую панель

**Входящие зависимости:**
- AppSettingsService
- PanelPackageManifest
- PanelPackageMapper
- PathHelper

**Исходящие зависимости:**
- Нет

**Основные классы:**
- PanelPackageService
- PanelExportResult
- PanelImportResult
- PanelImportPreview

**Основные функции:**
- ExportCurrentPanelAsync()
- ReadImportPreviewAsync()
- ImportIntoCurrentPanelAsync()

**Потоки данных:**
- Экспорт: CustomElement → PanelPackageElement → ZIP-архив
- Импорт: ZIP-архив → PanelPackageElement → CustomElement

## QuickNoteService
**Назначение:** Управление быстрыми заметками.

**Ответственность:**
- Загрузка заметки из файла QuickNote.md
- Сохранение заметки в файл QuickNote.md
- Проверка внешних изменений файла
- Сохранение копии при конфликте
- Открытие заметки в внешнем редакторе

**Входящие зависимости:**
- PathHelper
- QuickNoteMarkdown

**Исходящие зависимости:**
- Нет

**Основные классы:**
- QuickNoteService

**Основные функции:**
- LoadAsync()
- SaveAsync()
- ReadMarkdownAsync()
- HasExternalChanges()
- SaveConflictCopyAsync()
- OpenInEditor()

**Потоки данных:**
- Заметка хранится в QuickNote.md в папке данных приложения

## LocalizationService
**Назначение:** Локализация интерфейса приложения.

**Ответственность:**
- Применение культуры интерфейса
- Получение локализованных строк по ключу
- Форматирование локализованных строк
- Нормализация имени культуры

**Поддерживаемые культуры:**
- auto (автоматически по ОС)
- en
- ru
- de
- uk

**Входящие зависимости:**
- Нет

**Исходящие зависимости:**
- Нет

**Основные классы:**
- LocalizationService (static)
- LocalizedStringProvider
- LocExtension (MarkupExtension для XAML)

**Основные функции:**
- ApplyCulture()
- Get()
- Format()
- NormalizeCultureName()

## ContextStateHelper
**Назначение:** Работа с контекстами (панелями) кнопок.

**Ответственность:**
- Нормализация списка контекстов (всегда 8 контекстов)
- Нормализация активного контекста
- Получение списка включенных контекстов
- Получение относительного контекста (следующий/предыдущий)
- Обертка индекса контекста

**Входящие зависимости:**
- Нет

**Исходящие зависимости:**
- Нет

**Основные константы:**
- FixedContextCount = 8
- DefaultContextPrefix = "Panel "

**Основные функции:**
- NormalizeContexts()
- NormalizeActiveContextId()
- GetEnabledContexts()
- GetRelativeEnabledContextId()
- WrapIndex()

## BrowserHelper
**Назначение:** Работа с веб-браузерами.

**Ответственность:**
- Поиск исполняемого файла браузера (через реестр и стандартные пути)
- Получение пути к пользовательским данным браузера
- Получение списка профилей браузера (из Preferences для Chromium, из profiles.ini для Firefox)
- Определение системного браузера по умолчанию
- Переход к следующему профилю для ротации

**Поддерживаемые браузеры:**
- Chrome
- Edge
- Brave
- Yandex
- Opera
- OperaGX
- Vivaldi
- Firefox

**Входящие зависимости:**
- Нет

**Исходящие зависимости:**
- Нет

**Основные классы:**
- BrowserHelper (static)
- BrowserProfileInfo

**Основные функции:**
- GetExecutablePath()
- GetProfiles()
- GetSystemDefaultBrowser()
- GetUserDataPath()
- AdvanceProfile()

## PanelLayoutHelper
**Назначение:** Расчет геометрии панели.

**Ответственность:**
- Расчет размеров панели в зависимости от ориентации (вертикальная/горизонтальная)
- Расчет расположения системных и пользовательских кнопок
- Расчет количества полос (bands) для пользовательских кнопок (максимум 3)

**Интеграция с MainWindow:**
- `MainWindow.CalculateAvailableSize()` получает рабочую область целевого монитора, учитывает DPI и единый `PanelScreenPadding`.
- `MainWindow.ComputePanelMetrics()` собирает количество системных кнопок, количество кнопок по активным контекстам и вызывает `PanelLayoutHelper.Calculate()` сначала для определения `UserBands`, затем для финальных размеров с учетом скрытия separator между системным и пользовательским блоком.
- `MainWindow.ApplyPanelSizeConstraints(metrics)` только применяет готовые `PanelLayoutMetrics` к XAML-элементам. Он не должен заново считать доступную область или вызывать `PanelLayoutHelper.Calculate()`.
- `RefreshPanel()` и `UpdateOrientation()` используют один и тот же путь вычисления metrics, чтобы размеры панели и содержимого не расходились между обновлениями.

**Входящие зависимости:**
- Нет

**Исходящие зависимости:**
- Нет

**Основные константы:**
- ButtonOuterSize = 44
- SeparatorSize = 9
- PanelChrome = 8
- MaxUserBands = 3

**Основные классы:**
- PanelLayoutHelper (static)
- PanelLayoutMetrics (record)
- UserLayout (record)

**Основные функции:**
- Calculate()
- CalculateUserLayout()

## PanelPositionHelper
**Назначение:** Чистая математика позиционирования панели без зависимости от XAML-элементов `MainWindow`.

**Ответственность:**
- Расчет координат панели для сторон `Top`, `Bottom`, `Left`, `Right` в показанном и скрытом состоянии
- Центрирование панели в рабочей области монитора
- Выбор ближайшего края при перетаскивании drag handle с hysteresis для текущего края, чтобы панель не прыгала около углов
- Поиск индекса монитора по `DeviceName`

**Входящие зависимости:**
- `DockEdge`
- `System.Windows.Rect`
- `System.Drawing.Rectangle`

**Исходящие зависимости:**
- Нет

**Основные функции:**
- GetDockCoordinates()
- GetClosestDockEdge()
- FindScreenIndex()

---

# 6. Пользовательские функции

## Показ/скрытие панели
**Назначение:** Отображение и скрытие основной панели с кнопками.

**Где находится:** MainWindow.xaml.cs

**Какие компоненты участвуют:** MainWindow, NativeIntegrationService, NativeMethods

**Какие файлы реализуют функцию:** MainWindow.xaml.cs, NativeMethods.cs, NativeIntegrationService.cs

**Пошаговый сценарий выполнения:**
1. Пользователь наводит курсор на край экрана или нажимает глобальную горячую клавишу
2. Если курсор находится в зоне активации — запускается таймер
3. По истечении задержки — панель плавно появляется с анимацией
4. Для скрытия — пользователь кликает вне панели (обрабатывается mouse hook) или курсор покидает панель

**Какие данные используются:**
- _appSettings.Edge — сторона экрана
- _appSettings.ActivationZoneSizePercent — размер зоны активации
- _appSettings.ActivationDelayMs — задержка перед показом

**Что получает пользователь:** Плавно появляющаяся/скрывающаяся панель с кнопками.

## Управление пользовательскими кнопками
**Назначение:** Добавление, редактирование, удаление, дублирование, переименование и перетаскивание пользовательских кнопок.

**Где находится:** MainWindow.xaml.cs, SettingsWindow.xaml.cs

**Какие компоненты участвуют:** MainWindow, SettingsWindow, AppSettingsService

**Какие файлы реализуют функцию:** MainWindow.xaml.cs, SettingsWindow.xaml.cs, AppSettingsService.cs

**Пошаговый сценарий выполнения (добавление кнопки):**
1. Пользователь нажимает кнопку "+"
2. Открывается SettingsWindow для создания новой кнопки
3. Пользователь заполняет параметры кнопки
4. Нажимает "Сохранить"
5. Кнопка добавляется в панель и сохраняется в настройках

**Какие данные используются:** CustomElement, AppSettings

**Что получает пользователь:** Возможность полного управления своими кнопками.

## Переключение контекстов
**Назначение:** Переключение между различными панелями (контекстами) кнопок (8 контекстов).

**Где находится:** MainWindow.xaml.cs, ContextStateHelper.cs

**Какие компоненты участвуют:** MainWindow, ContextStateHelper, AppSettingsService

**Какие файлы реализуют функцию:** MainWindow.xaml.cs, ContextStateHelper.cs, AppSettingsService.cs

**Пошаговый сценарий выполнения:**
1. Пользователь выбирает контекст из контекстного меню панели или использует горячие клавиши
2. Обновляется _appSettings.ActiveContextId
3. Вызывается RefreshPanel() для обновления отображаемых кнопок
4. Настройки сохраняются

**Какие данные используются:** PanelContext, AppSettings.Contexts, AppSettings.ActiveContextId

**Что получает пользователь:** Быстрый доступ к разным наборам кнопок.

## Выполнение действий
**Назначение:** Выполнение различных типов действий (веб, программы, файлы, папки, скрипты, команды, горячие клавиши).

**Где находится:** ActionService.cs

**Какие компоненты участвуют:** ActionService, BrowserHelper, NativeMethods

**Какие файлы реализуют функцию:** ActionService.cs, BrowserHelper.cs, NativeMethods.cs

**Пошаговый сценарий выполнения (веб-действие):**
1. Пользователь нажимает на кнопку с веб-действием
2. ActionService.ExecuteCustomActionAsync() вызывает ExecuteWebActionAsync()
3. Если включена ротация профилей — выбирается следующий профиль
4. Создается ProcessStartInfo с нужными аргументами (профиль, инкогнито, app mode)
5. Запускается процесс браузера
6. Если нужно — открывается в полноэкранном режиме (отправка F11 через SendInput)

**Какие данные используются:** CustomElement, BrowserType, BrowserProfileInfo

**Что получает пользователь:** Выполнение нужного действия в один клик.

## Импорт/экспорт панелей
**Назначение:** Сохранение текущей панели в ZIP-архив и загрузка панели из ZIP-архива.

**Где находится:** MainWindow.xaml.cs, PanelPackageService.cs

**Какие компоненты участвуют:** MainWindow, PanelPackageService, AppSettingsService, PanelPackageManifest, PanelPackageMapper

**Какие файлы реализуют функцию:** MainWindow.xaml.cs, PanelPackageService.cs, PanelPackageManifest.cs, PanelPackageMapper.cs

**Пошаговый сценарий выполнения (экспорт):**
1. Пользователь выбирает "Экспорт текущей панели" из контекстного меню
2. Открывается диалог сохранения файла
3. PanelPackageService.ExportCurrentPanelAsync() сериализует кнопки текущего контекста в manifest.json
4. Пользовательские иконки копируются в папку icons/ в архиве
5. Все упаковывается в ZIP-архив с расширением .aitebarpanel

**Пошаговый сценарий выполнения (импорт):**
1. Пользователь выбирает "Импорт в текущую панель" из контекстного меню
2. Открывается диалог выбора файла
3. PanelPackageService.ReadImportPreviewAsync() показывает превью импорта
4. PanelPackageService.ImportIntoCurrentPanelAsync() извлекает архив, копирует иконки в локальное хранилище, добавляет кнопки в текущий контекст

**Какие данные используются:** PanelPackageManifest, PanelPackageElement, List<CustomElement>

**Что получает пользователь:** Возможность переносить свои панели между компьютерами или делиться ими.

## Использование системных утилит
**Назначение:** Быстрый доступ к встроенным утилитам Windows и функциям AiteBar.

**Где находится:** MainWindow.xaml.cs, ActionService.cs

**Какие компоненты участвуют:** MainWindow, ActionService

**Какие файлы реализуют функцию:** MainWindow.xaml.cs, ActionService.cs

**Список системных утилит:**
- Поиск (Google)
- Скриншот (ms-screenclip:)
- Запись видео (ms-screenclip:?type=recording)
- Калькулятор (calc.exe)
- Проводник (explorer.exe)
- Загрузки (shell:Downloads)
- Таймер и секундомер (TimerStopwatchWindow)
- File Sorter (FileSorterWindow)
- Icon Converter (IconConverterWindow)
- Пипетка цвета и цветовой выбор (ScreenColorPickerWindow + ColorPickerDialog)
- Quick Note (QuickNoteWindow)
- QR Code Generator (QRCodeGeneratorWindow)
- Clipboard Manager (ClipboardManagerWindow)
- Show Desktop (Win32 `#(Win+D)` via SendInput)
- Apps Folder (`shell:AppsFolder`)
- Copilot (`microsoft-edge://?ux=copilot&tcp=1&source=taskbar`)
- Обработка текста — AI Text Processing (TextProcessingWindow)
- Дзен-редактор (ZenEditorWindow)

**Пошаговый сценарий выполнения (скриншот):**
1. Пользователь нажимает кнопку скриншота
2. ActionService.StartScreenshotAsync() запускает процесс "ms-screenclip:"
3. Открывается встроенный инструмент Windows для создания скриншотов

**Что получает пользователь:** Быстрый доступ к часто используемым системным функциям.

## Быстрые заметки
**Назначение:** Создание и редактирование быстрых заметок с поддержкой Markdown.

**Где находится:** QuickNoteWindow.xaml.cs, QuickNoteService.cs

**Какие компоненты участвуют:** QuickNoteWindow, QuickNoteService, QuickNoteMarkdown

**Какие файлы реализуют функцию:** QuickNoteWindow.xaml.cs, QuickNoteService.cs, QuickNoteMarkdown.cs

**Пошаговый сценарий выполнения:**
1. Пользователь нажимает кнопку быстрых заметок
2. ActionService.StartQuickNoteAsync() открывает QuickNoteWindow
3. QuickNoteService загружает заметку из QuickNote.md
4. Пользователь редактирует заметку
5. Заметка автоматически сохраняется в QuickNote.md

**Какие данные используются:** QuickNote.md (хранится в папке данных приложения)

**Что получает пользователь:** Удобный редактор быстрых заметок с поддержкой Markdown.

---

# 7. Потоки выполнения

## Поток выполнения: Нажатие на пользовательскую кнопку

```
Пользователь
  ↓
Нажатие на кнопку в MainWindow
  ↓
Обработчик клика в MainWindow.xaml.cs
  ↓
ActionService.ExecuteCustomActionAsync(element)
  ↓
Определение типа действия (ActionType)
  ↓
┌─────────────────────────────────────────┐
│ В зависимости от ActionType:            │
│  • Web → ExecuteWebActionAsync()       │
│  • Hotkey → ExecuteHotkey()             │
│  • Program/File/Folder → Process.Start()│
│  • ScriptFile → StartScriptFileAsync()  │
│  • Command → ExecuteCommand()            │
└─────────────────────────────────────────┘
  ↓
Выполнение действия
  ↓
Возврат ActionExecutionResult
```

## Поток выполнения: Загрузка настроек при запуске

```
Запуск приложения
  ↓
MainWindow конструктор
  ↓
PathHelper.EnsureDirectories()
  ↓
AppSettingsService.LoadAsync()
  ↓
Проверка наличия settings.json
  ├─ Если есть → десериализация → NormalizeAppState()
  ├─ Если нет, но есть custom_buttons.json → миграция → NormalizeAppState()
  └─ Если нет → NormalizeAppState() с значениями по умолчанию
  ↓
SaveAsync() (если были изменения при нормализации)
  ↓
Настройки готовы к использованию
```

## Поток выполнения: Показ панели по горячей клавише

```
Пользователь нажимает глобальную горячую клавишу
  ↓
Windows отправляет сообщение WM_HOTKEY
  ↓
WndProc в MainWindow.xaml.cs получает сообщение
  ↓
Проверка id горячей клавиши
  ↓
Если HOTKEY_ID → вызов ToggleDock()
  ↓
ToggleDock() проверяет текущее состояние (_shown)
  ├─ Если скрыта → ShowDock()
  └─ Если показана → HideDock()
  ↓
Панель появляется или скрывается с анимацией
```

## Поток выполнения: Экспорт панели

```
Пользователь выбирает "Экспорт текущей панели"
  ↓
Открывается SaveFileDialog
  ↓
PanelPackageService.ExportCurrentPanelAsync(packagePath)
  ↓
Создается временная директория
  ↓
Создается manifest.json с информацией о панели и кнопках
  ↓
Копируются пользовательские иконки в папку icons/
  ↓
Создается ZIP-архив .aitebarpanel
  ↓
Возвращается PanelExportResult
```

---

# 8. Frontend

## Компоненты
Основные окна (WPF Windows):
- **MainWindow** — основная панель с кнопками
- **SettingsWindow** — редактирование отдельной кнопки
- **AppSettingsWindow** — общие настройки приложения и сведения «О программе»
- **QuickNoteWindow** — быстрые заметки с Markdown
- **IconPickerWindow** — выбор иконки
- **DarkDialog** — диалоговое окно с подтверждением
- **TextPromptDialog** — диалоговое окно для ввода текста
- **RotationProfileSelectionWindow** — выбор профилей для ротации
- **ScreenColorPickerWindow** — цветовой пикер

Дополнительно:
- **DarkWindow** — базовый класс для темных окон
- **DarkDialog** — темное диалоговое окно

## Layouts
- **MainWindow**:
  - RootBorder — корневой контейнер с закругленными углами
  - MainPanel — основная панель
  - FixedPanel — панель с системными кнопками
  - UserButtonsPanel — панель с пользовательскими кнопками
  - ControlBlock — блок с кнопкой добавления

## State Management
Состояние приложения хранится в:
- **AppSettings** — в AppSettingsService
- **MainWindow** — локальное состояние панели (показана/скрыта, перетаскивание, и т.д.)
- Нет внешних библиотек управления состоянием (используются обычные C# свойства и события)

## Глобальное состояние
- AppSettingsService имеет событие SettingsChanged для уведомления об изменениях настроек
- LocalizationService имеет событие CultureChanged для уведомления об изменении языка

## Кэширование
- Кэш изображений кнопок в MainWindow (`_buttonImageCache`)

## Жизненный цикл компонентов
- Приложение запускается → создается App → создается MainWindow
- MainWindow инициализирует сервисы и tray icon
- При закрытии MainWindow → приложение завершается
- Окна настроек открываются модально как диалоги

---

# 9. Backend
Так как это desktop-приложение на WPF, классического backend в веб-понимании нет. Весь код выполняется локально на компьютере пользователя.

## Сервисы (бизнес-логика):
1. **ActionService** — выполнение действий
2. **AppSettingsService** — управление настройками
3. **PanelPackageService** — импорт/экспорт панелей
4. **QuickNoteService** — управление быстрыми заметками
5. **LocalizationService** — локализация
6. **NativeIntegrationService** — native integration

## Валидация
Валидация целей действий выполняется в **ActionTargetHelper.cs**.

## Фоновые задачи
Нет явных фоновых задач. Все операции выполняются синхронно или через async/await без отдельных фоновых потоков.

---

# 10. API
API отсутствует, так как это desktop-приложение без сетевого взаимодействия (кроме открытия сайтов в браузере).

---

# 11. База данных
База данных отсутствует. Все данные хранятся в JSON-файлах в папке `%APPDATA%\Codebdbd\Aite Bar\`.

## Файлы данных
- **settings.json** — основные настройки и пользовательские кнопки
- **custom_buttons.json** — старый формат файла с кнопками (для миграции)
- **QuickNote.md** — быстрая заметка
- **error.log** — лог ошибок
- **Icons/** — папка с пользовательскими иконками

---

# 12. Модели данных

## PanelContext
**Назначение:** Представляет контекст (панель) кнопок.

**Поля:**
- Id (string) — уникальный идентификатор контекста
- Name (string) — название контекста
- IconGlyph (string) — глиф иконки контекста
- IsEnabled (bool) — включен ли контекст

**Использование в системе:** Используется в AppSettings для хранения списка контекстов. Нормализуется ContextStateHelper до 8 контекстов.

---

## HotkeyBinding
**Назначение:** Представляет комбинацию горячих клавиш.

**Поля:**
- Ctrl (bool) — нажат ли Ctrl
- Alt (bool) — нажат ли Alt
- Shift (bool) — нажат ли Shift
- Win (bool) — нажат ли Win
- Key (string) — основная клавиша

**Использование в системе:** Используется в AppSettings для глобальных горячих клавиш приложения и встроенных утилит.

---

## CustomElement
**Назначение:** Представляет пользовательскую кнопку.

**Поля:**
- Id (string) — уникальный идентификатор кнопки
- Name (string) — название кнопки
- Icon (string) — глиф иконки
- IconFont (string) — шрифт иконки
- Color (string) — цвет иконки
- ActionType (string) — тип действия (Web, Hotkey, Program, File, Folder, ScriptFile, Command)
- ActionValue (string) — значение действия (URL, путь, команда)
- Browser (BrowserType) — браузер для веб-действий
- ChromeProfile (string) — профиль браузера
- RotationProfilePaths (List<string>) — пути к профилям для ротации
- IsAppMode (bool) — открывать в app mode
- IsIncognito (bool) — открывать в инкогнито
- UseRotation (bool) — использовать ротацию профилей
- OpenFullscreen (bool) — открывать в полноэкранном режиме
- IsTopmost (bool) — окно поверх всех
- LastUsedProfile (string) — последний использованный профиль
- Alt/Ctrl/Shift/Win (bool) — модификаторы payload для действия Hotkey
- Key (string) — основная клавиша payload для действия Hotkey
- ImagePath (string) — путь к кастомному изображению
- ContextId (string) — идентификатор контекста, к которому принадлежит кнопка

**Использование в системе:** Основная модель данных для пользовательских кнопок. Хранится в AppSettings и управляется через AppSettingsService.

---

## AppSettings
**Назначение:** Представляет все настройки приложения.

**Поля:**
- GlobalHotkeyCtrl/Alt/Shift/Win (bool) — глобальная горячая клавиша для показа панели
- GlobalHotkeyKey (string) — основная клавиша глобальной горячей клавиши
- ShowPresetSearch/Screenshot/Video/Calc/Explorer/Downloads/ColorPicker/QuickNote/FileSorter/IconConverter/QRCodeGenerator/ClipboardManager/TimerStopwatch/ShowDesktop/AppsFolder/Copilot (bool) — показывать ли соответствующие системные кнопки и встроенные утилиты
- ClipboardManagerPersistHistory (bool) — сохранять ли историю Clipboard Manager между сессиями
- FileSorterHotkey/IconConverterHotkey/QuickNoteHotkey/ColorPickerHotkey/TimerStopwatchHotkey/QRCodeGeneratorHotkey/ClipboardManagerHotkey (HotkeyBinding) — горячие клавиши всех встроенных utility-окон
- QuickNoteThemeId (string) — тема быстрых заметок
- Edge (DockEdge) — сторона экрана для панели
- MonitorIndex (int) — индекс монитора
- ActivationZoneSizePercent (double) — размер зоны активации
- PanelSizePercent (double) — размер панели
- ActivationDelayMs (int) — задержка перед показом панели
- UiCulture (string) — язык интерфейса
- Contexts (List<PanelContext>) — список контекстов (8 контекстов)
- ActiveContextId (string) — активный контекст
- NextContextHotkey/PreviousContextHotkey/AddButtonHotkey (HotkeyBinding) — горячие клавиши
- Elements (List<CustomElement>) — список пользовательских кнопок

**Использование в системе:** Основная модель настроек приложения. Загружается и сохраняется через AppSettingsService.

---

## BrowserProfileInfo
**Назначение:** Представляет информацию о профиле браузера.

**Поля:**
- DisplayName (string) — отображаемое имя профиля
- ProfilePath (string) — путь к профилю
- ProfileName (string) — имя папки профиля (вычисляемое)

**Использование в системе:** Используется в BrowserHelper для получения списка профилей браузера.

---

## PanelPackageManifest
**Назначение:** Представляет manifest.json в ZIP-архиве панели.

**Поля:**
- FormatVersion (int) — версия формата (текущая 1)
- ExportedAt (DateTime) — дата экспорта
- App (PanelPackageAppInfo) — информация о приложении
- Panel (PanelPackagePanelInfo) — информация о панели
- Elements (List<PanelPackageElement>) — список кнопок

**Использование в системе:** Используется PanelPackageService для экспорта и импорта панелей.

---

## PanelPackageElement
**Назначение:** Представляет кнопку в manifest.json панели.

**Поля:**
- Name (string) — название кнопки
- ActionType (string) — тип действия
- ActionValue (string) — значение действия
- Browser (BrowserType) — браузер
- ChromeProfile (string) — профиль браузера
- RotationProfilePaths (List<string>) — пути к профилям для ротации
- IsAppMode (bool) — app mode
- IsIncognito (bool) — инкогнито
- UseRotation (bool) — ротация профилей
- OpenFullscreen (bool) — полноэкранный режим
- IsTopmost (bool) — поверх всех окон
- Alt/Ctrl/Shift/Win (bool) — модификаторы горячей клавиши
- Key (string) — клавиша горячей клавиши
- Icon (string) — глиф иконки
- IconFont (string) — шрифт иконки
- Color (string) — цвет иконки
- Image (PanelPackageImageInfo?) — информация о кастомном изображении

**Использование в системе:** Используется PanelPackageService для экспорта и импорта панелей.

---

# 13. Управление состоянием
Управление состоянием в приложении организовано простым образом без внешних библиотек:

- **Глобальное состояние:** Хранится в `AppSettings` внутри `AppSettingsService`. Состояние сохраняется в JSON-файл.
- **Локальное состояние:** Локальные переменные и свойства в окнах (например, `_shown`, `_isAnimating` в MainWindow).
- **События:** 
  - `AppSettingsService.SettingsChanged` — для уведомления об изменениях настроек
  - `LocalizationService.CultureChanged` — для уведомления об изменении языка
- **Кэширование:** Кэш изображений кнопок в MainWindow (`_buttonImageCache`).

---

# 14. Авторизация и безопасность
Авторизация и аутентификация отсутствуют, так как это локальное desktop-приложение без пользовательских учетных записей.

**Безопасность:**
- При выполнении команд (`ActionType.Command`) показывается диалог подтверждения
- Скрипты запускаются с учетом расширения файла
- Ошибки логируются в локальный файл, не содержащий конфиденциальных данных
- При импорте панелей проверяется безопасность путей к изображениям (PanelPackageMapper.IsPackagedImagePathSafe)

---

# 15. Интеграции
## Интеграция с Windows Shell
**Назначение:** Взаимодействие с операционной системой Windows.

**Используемые методы:** Win32 API через P/Invoke (NativeMethods.cs).

**Передаваемые данные:** Окна, сообщения, ввод пользователя.

**Точки вызова:** MainWindow.xaml.cs, NativeIntegrationService.cs, ActionService.cs.

**Обработка ошибок:** Ошибки логируются через Logger.

**Возможности:**
- Глобальные горячие клавиши (RegisterHotKey/UnregisterHotKey)
- Low-level mouse hook (SetWindowsHookEx)
- Позиционирование окон (SetWindowPos)
- Отправка ввода (SendInput)
- Установка окна на передний план (SetForegroundWindow)

---

## Интеграция с веб-браузерами
**Назначение:** Открытие URL в различных браузерах с поддержкой профилей, инкогнито и app mode.

**Используемые методы:** Process.Start(), работа с реестром Windows, чтение файлов настроек браузеров (Preferences для Chromium, profiles.ini для Firefox).

**Передаваемые данные:** URL, профиль браузера, аргументы командной строки.

**Точки вызова:** ActionService.cs, BrowserHelper.cs.

**Обработка ошибок:** Ошибки логируются через Logger.

**Поддерживаемые браузеры:**
- Chrome
- Edge
- Brave
- Yandex
- Opera
- OperaGX
- Vivaldi
- Firefox

---

# 16. Конфигурация

## Конфигурационные файлы
Все конфигурационные файлы хранятся в `%APPDATA%\Codebdbd\Aite Bar\`:
- **settings.json** — основной файл настроек (AppSettings)
- **custom_buttons.json** — старый формат файла с кнопками (для миграции)
- **QuickNote.md** — быстрая заметка
- **error.log** — лог ошибок
- **Icons/** — папка с пользовательскими иконками

## Параметры настроек (AppSettings)
См. раздел "Модели данных" → AppSettings.

## Значения по умолчанию
Значения по умолчанию задаются в конструкторах моделей (Models.cs) и в методе NormalizeAppState() в AppSettingsService.

**Основные значения по умолчанию:**
- Глобальная горячая клавиша: Alt + D4
- Сторона панели: Top
- Язык интерфейса: auto (по ОС)
- Включенные системные кнопки: Search, Screenshot, Video, Calculator, Explorer, Downloads, File Sorter, Icon Converter, Timer/Stopwatch, Show Desktop, Apps Folder, Copilot

---

# 17. Производительность
## Оптимизации
- **Ленивая загрузка:** Нет, все компоненты загружаются при запуске.
- **Кэширование:** Кэш изображений кнопок в MainWindow (`_buttonImageCache`).
- **Потокобезопасность:** Сохранение настроек защищено SemaphoreSlim в AppSettingsService.

---

# 18. Обработка ошибок
## Логирование
Ошибки логируются в файл `%APPDATA%\Codebdbd\Aite Bar\error.log` с помощью статического класса Logger.cs.

**Ограничения лога:**
- Максимальный размер файла: 1 МБ
- При превышении размера создается бэкап error.log.bak и новый файл

## Fallback-сценарии
- Если браузер не найден — используется Chrome по умолчанию
- Если файл настроек поврежден — создаются настройки по умолчанию
- Если иконка не найдена — используется иконка по умолчанию
- Если культура не поддерживается — используется английский

## Уведомления пользователя
- Ошибки при выполнении действий не показываются пользователю (только логируются)
- Для опасных действий (например, выполнение команд) показывается диалог подтверждения
- При импорте/экспорте могут показываться диалоги об успехе/ошибке

---

# 19. Сборка и запуск

## Установка зависимостей
Зависимости являются частью .NET 10 SDK и WPF, дополнительных пакетов NuGet не требуется. Требуется установленный .NET 10 SDK.

## Настройка окружения
Требуется:
- Windows 10 или Windows 11
- .NET 10 SDK

## Сборка
```powershell
dotnet build .\AiteBar.sln -c Release
```

## Запуск
```powershell
dotnet run --project .\AiteBar\AiteBar.csproj
```
Или запустите скомпилированный exe-файл из bin\Release\net10.0-windows\.

## Тестирование
Основной вариант:
```powershell
dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
```

Fallback для случаев с WPF temp-файлами:
```powershell
dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll
```

## Production deployment
Сборка инсталлятора:
```powershell
.\installer\Build-Installer.ps1
```
Инсталлятор будет создан в artifacts\installer\.

---

# 20. CI/CD
CI/CD настроен через GitHub Actions:

- `.github/workflows/build-test.yml` — Release build, тесты и coverage summary/artifact для push/PR в `main` и `master`.
- `.github/workflows/codeql.yml` — CodeQL security-and-quality analysis для C#.
- `.github/workflows/release.yml` — проверка версии, сборка инсталлятора, checksum и публикация GitHub Release для `vX.Y.Z` tag.

Зависимости NuGet и GitHub Actions отслеживаются через `.github/dependabot.yml`.

---

# 21. Карта зависимостей

```
App → MainWindow
MainWindow → AppSettingsService
MainWindow → ActionService
MainWindow → PanelPackageService
MainWindow → NativeIntegrationService
MainWindow → SettingsWindow
MainWindow → AppSettingsWindow
MainWindow → QuickNoteWindow
MainWindow → IconPickerWindow
MainWindow → Other Windows...

ActionService → AppSettingsService
ActionService → BrowserHelper
ActionService → ProfileRotationHelper
ActionService → NativeMethods
ActionService → QuickNoteWindow
ActionService → ScreenColorPickerWindow

AppSettingsService → PathHelper
AppSettingsService → ContextStateHelper
AppSettingsService → LocalizationService

PanelPackageService → AppSettingsService
PanelPackageService → PanelPackageManifest
PanelPackageService → PanelPackageMapper
PanelPackageService → PathHelper

QuickNoteService → PathHelper
QuickNoteService → QuickNoteMarkdown

BrowserHelper → (нет зависимостей)
PanelLayoutHelper → (нет зависимостей)
ContextStateHelper → (нет зависимостей)
ProfileRotationHelper → (нет зависимостей)
PathHelper → (нет зависимостей)
Logger → PathHelper
NativeMethods → (нет зависимостей, P/Invoke)
LocalizationService → (нет зависимостей)
QuickNoteMarkdown → (нет зависимостей)
PanelPackageMapper → (нет зависимостей)
```

---

# 22. Индекс исходного кода

| Файл | Назначение | Важность |
|------|------------|----------|
| Models.cs | Модели данных (AppSettings, CustomElement, и т.д.) | Critical |
| MainWindow.xaml / MainWindow.xaml.cs | Основное окно приложения | Critical |
| AppSettingsService.cs | Управление настройками | Critical |
| ActionService.cs | Выполнение действий | Critical |
| NativeMethods.cs | Win32 interop | Critical |
| PathHelper.cs | Пути к данным | High |
| Logger.cs | Логирование ошибок | High |
| BrowserHelper.cs | Работа с браузерами | High |
| PanelLayoutHelper.cs | Расчет геометрии панели | High |
| PanelPackageService.cs | Импорт/экспорт панелей | High |
| ContextStateHelper.cs | Работа с контекстами | High |
| QuickNoteService.cs | Быстрые заметки | Medium |
| LocalizationService.cs | Локализация | Medium |
| SettingsWindow.xaml.cs | Редактирование кнопки | Medium |
| AppSettingsWindow.xaml.cs | Общие настройки | Medium |
| ProfileRotationHelper.cs | Ротация профилей | Medium |
| IconHelper.cs / FontHelper.cs | Работа с иконками | Medium |
| NativeIntegrationService.cs | Native integration | Medium |
| PanelPackageManifest.cs / PanelPackageMapper.cs | Импорт/экспорт панелей | Medium |
| QuickNoteMarkdown.cs | Markdown для заметок | Medium |
| Other Windows (.xaml/.cs) | Вспомогательные окна | Low |
| AssemblyInfo.cs | Информация о сборке | Low |

---

# 23. Скрытые возможности

## Debug-функции
Явных debug-функций или feature flags нет.

## Экспериментальные механизмы
Нет экспериментальных механизмов.

## Внутренние инструменты
- Логирование ошибок в error.log (включено всегда)
- Возможность открыть папку с данными через проводник (клик на иконке проводника в системных кнопках)
- Логирование всех ошибок через Logger.cs

---

# 24. Критически важные компоненты

## MainWindow
**Почему критичен:** Это основное окно приложения, без него панель не работает. Содержит логику показа/скрытия панели, tray icon, глобальных горячих клавиш и управления кнопками.

## AppSettingsService
**Почему критичен:** Управляет всеми настройками и пользовательскими кнопками. Без него приложение не может загрузить или сохранить конфигурацию.

## ActionService
**Почему критичен:** Выполняет все пользовательские действия и системные утилиты. Без него кнопки не будут работать.

## NativeMethods
**Почему критичен:** Содержит P/Invoke объявления для Win32 API, которые используются для глобальных горячих клавиш, mouse hooks, позиционирования окон и отправки ввода. Без них интеграция с Windows не работает.

---

# 25. Выводы

## Архитектурные сильные стороны
- Простая и понятная архитектура без избыточной сложности
- Хорошая разделение ответственности между сервисами и helper-классами
- Unit-тесты для ключевых компонентов
- Поддержка локализации (4 языка)
- Кросс-браузерность с поддержкой профилей
- Плавная анимация и отзывчивый UI
- ZIP-архивирование панелей с пользовательскими иконками
- 8 фиксированных контекстов для организации кнопок
- Быстрые заметки с поддержкой Markdown

## Потенциальные проблемы
- Основная логика UI и системного поведения сосредоточена в MainWindow.xaml.cs (класс очень большой, >1000 строк)
- Нет разделения на ViewModel и View (нет MVVM)
- Нет инверсии контрола (IoC) или контейнера зависимостей
- Логирование только ошибок, нет информационных или отладочных сообщений


## Технический долг
- Рефакторинг MainWindow.xaml.cs — разбить на более мелкие классы
- Внедрение MVVM для лучшей тестируемости и разделения ответственности
- Добавление более подробного логирования

## AI provider gateway

`AiGateway` — единая точка доступа будущих AI-утилит к внешним моделям. Он читает только несекретную маршрутизацию из `AppSettingsService`, выбирает подключение по provider order и priority, учитывает общий `QuotaScopeId` и передаёт запрос в `AiProviderClient`. После `429` все подключения того же quota scope временно пропускаются. `401` блокирует только конкретный ключ.

`AiProviderClient` нормализует каталоги OpenRouter, Cerebras, Gemini, Groq, GitHub Models и Mistral в `AiModelDescriptor`. OpenAI-compatible сервисы используют общий transport; Gemini имеет отдельное преобразование request/response. Секреты получает `IAiCredentialStore`; production-реализация `WindowsAiCredentialStore` работает с Windows Credential Manager. API-ключ не является частью `AppSettings` и не сериализуется в JSON.

Пользовательская настройка и ограничения описаны в [AI_PROVIDERS.md](AI_PROVIDERS.md).

## Рекомендации по развитию проекта
1. Разбить MainWindow.xaml.cs на несколько специализированных классов (например, PanelVisibilityManager, TrayIconManager, HotkeyManager, ButtonPanelManager)
2. Внедрить шаблон MVVM с использованием CommunityToolkit.Mvvm или аналогичной библиотеки
3. Добавить более подробное логирование (информационные, отладочные сообщения)
4. Добавить больше unit-тестов для покрытия всех сценариев
5. Рассмотреть внедрение контейнера зависимостей для улучшения тестируемости и расширяемости
