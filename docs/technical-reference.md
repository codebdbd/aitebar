# AiteBar Technical Reference

Этот документ хранит технические детали, которые не должны перегружать пользовательское руководство. Пользовательская инструкция находится в [USER_MANUAL.md](USER_MANUAL.md).

## Назначение

AiteBar - Windows desktop-утилита на `.NET 10` и WPF. Приложение показывает скрываемую edge-панель, работает в фоне, использует tray-значок, глобальные hotkey, Win32 interop и локальное хранилище настроек в профиле пользователя.

## Платформа

- Runtime: `.NET 10`
- Target: `net10.0-windows`
- UI: WPF
- Tray: Windows Forms `NotifyIcon`
- Системная интеграция: Win32 API для hotkey, mouse hook, позиционирования окон и отправки клавиатурного ввода
- Тесты: xUnit, `Microsoft.NET.Test.Sdk`, `coverlet.collector`

## Установка и автозапуск

Стандартный установщик ставит программу в `Program Files`, создаёт группу в меню Пуск и может добавить автозапуск текущего пользователя.

Автозапуск хранится в:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

## Один экземпляр приложения

AiteBar запускается как один экземпляр. Если пользователь запускает приложение повторно, второй экземпляр не стартует, а Windows показывает информационное сообщение о том, что AiteBar уже работает.

## Локальные данные

Папка данных:

```text
%AppData%\Codebdbd\Aite Bar
```

| Файл или папка | Назначение |
|---|---|
| `settings.json` | Основные настройки приложения, панелей и пользовательских кнопок |
| `custom_buttons.json` | Старый формат кнопок; используется для миграции, если `settings.json` отсутствует |
| `QuickNote.md` | Файл быстрой заметки |
| `clipboard_history.json` | История Clipboard Manager, если включено сохранение истории |
| `Icons` | Пользовательские и импортированные иконки |
| `error.log` | Текущий журнал ошибок |
| `error.log.bak` | Резервная копия журнала после ротации |

`error.log` ротируется при достижении примерно 1 МБ.

## Модель настроек

Основные поля настроек:

| Параметр | Значение по умолчанию | Описание |
|---|---:|---|
| `UiCulture` | `auto` | Язык интерфейса |
| `Edge` | `Top` | Сторона панели |
| `MonitorIndex` | `0` | Индекс монитора |
| `ActivationZoneSizePercent` | `30` | Размер зоны активации |
| `PanelSizePercent` | `80` | Размер панели |
| `ActivationDelayMs` | `150` | Задержка появления |
| `GlobalHotkeyCtrl` | `false` | Модификатор Ctrl для показа панели |
| `GlobalHotkeyAlt` | `true` | Модификатор Alt для показа панели |
| `GlobalHotkeyShift` | `false` | Модификатор Shift для показа панели |
| `GlobalHotkeyWin` | `false` | Модификатор Win для показа панели |
| `GlobalHotkeyKey` | `D4` | Клавиша показа панели |
| `NextContextHotkey` | не назначено | Следующая панель |
| `PreviousContextHotkey` | не назначено | Предыдущая панель |
| `AddButtonHotkey` | не назначено | Добавить кнопку |
| `IconConverterHotkey` | не назначено | Запустить Icon Converter |
| `ClipboardManagerHotkey` | не назначено | Запустить Clipboard Manager |
| `TextProcessingHotkey` | не назначено | Запустить Обработку текста |
| `ZenEditorHotkey` | не назначено | Запустить Дзен-редактор |
| `PromptBuilderHotkey` | не назначено | Запустить Конструктор промптов |
| `FileSorterHotkey` | не назначено | Запустить File Sorter |
| `QuickNoteHotkey` | не назначено | Запустить Quick Note |
| `ColorPickerHotkey` | не назначено | Запустить Color Picker |
| `TimerStopwatchHotkey` | не назначено | Запустить таймер и секундомер |
| `ShowPresetSearch` | `true` | Показывать поиск |
| `ShowPresetScreenshot` | `true` | Показывать скриншот |
| `ShowPresetVideo` | `true` | Показывать запись видео |
| `ShowPresetCalc` | `true` | Показывать калькулятор |
| `ShowPresetExplorer` | `true` | Показывать проводник |
| `ShowPresetDownloads` | `true` | Показывать загрузки |
| `ShowPresetTimerStopwatch` | `true` | Показывать таймер и секундомер |
| `ShowPresetColorPicker` | `false` | Показывать выбор цвета |
| `ShowPresetQuickNote` | `false` | Показывать Quick Note |
| `ShowPresetFileSorter` | `true` | Показывать File Sorter |
| `ShowPresetIconConverter` | `true` | Показывать Icon Converter |
| `ShowPresetQRCodeGenerator` | `false` | Показывать QR Code Generator |
| `ShowPresetClipboardManager` | `false` | Показывать Clipboard Manager |
| `ShowPresetShowDesktop` | `true` | Показывать Show Desktop |
| `ShowPresetAppsFolder` | `true` | Показывать Apps Folder |
| `ShowPresetCopilot` | `true` | Показывать Copilot |
| `ShowPresetPromptBuilder` | `false` | Показывать Конструктор промптов (AI) на панели |
| `ShowPresetTextProcessing` | `true` | Показывать Обработку текста (AI) |
| `ShowPresetZenEditor` | `true` | Показывать Дзен-редактор |
| `ClipboardManagerPersistHistory` | `true` | Сохранять историю Clipboard Manager между сессиями |
| `QRCodeGeneratorHotkey` | не назначено | Запустить QR Code Generator |
| `TimerSoundEnabled` | `true` | Звук окончания таймера |
| `TimerIsStopwatchMode` | `false` | Последний выбранный режим: таймер или секундомер |
| `TimerDuration` | `00:05:00` | Последняя длительность таймера |
| `QuickNoteThemeId` | `dark` | Тема Quick Note |
| `QuickNotePinned` | `false` | Закрепить окно Quick Note (не закрывать при потере фокуса) |
| `QuickNoteLeft` | — | Координата X окна Quick Note |
| `QuickNoteTop` | — | Координата Y окна Quick Note |
| `QuickNoteWidth` | — | Ширина окна Quick Note |
| `QuickNoteHeight` | — | Высота окна Quick Note |
| `CheckForUpdatesEnabled` | `true` | Проверять наличие обновлений при ручном вызове проверки |
| `ShowTaskbarPositionIndicator` | `true` | Показывать указатель положения панели на краю экрана |
| `TaskbarIndicatorPositionX` | — | Относительная координата X указателя положения панели (0..1) |
| `TaskbarIndicatorPositionY` | — | Относительная координата Y указателя положения панели (0..1) |
| `TextProcessingLeft` | — | Координата X окна Обработки текста |
| `TextProcessingTop` | — | Координата Y окна Обработки текста |
| `TextProcessingWidth` | — | Ширина окна Обработки текста |
| `TextProcessingHeight` | — | Высота окна Обработки текста |
| `TextProcessingWindowState` | — | Состояние окна Обработки текста (Normal/Maximized/Minimized) |
| `TextProcessingLastMode` | `0` | Последний выбранный предустановленный режим Обработки текста |
| `TextProcessingSelectedConnectionId` | — | Идентификатор последнего выбранного AI-подключения |
| `TextProcessingSelectedModelId` | — | Идентификатор последней выбранной AI-модели |
| `TextProcessingSelectedProviderId` | — | Идентификатор последнего выбранного AI-провайдера |
| `TextProcessingIsAutoModel` | `true` | Автовыбор модели в Обработке текста по стоимости и контексту |
| `PromptBuilderLeft` | — | Координата X окна Конструктора промптов |
| `PromptBuilderTop` | — | Координата Y окна Конструктора промптов |
| `PromptBuilderWidth` | — | Ширина окна Конструктора промптов |
| `PromptBuilderHeight` | — | Высота окна Конструктора промптов |
| `PromptBuilderWindowState` | — | Состояние окна Конструктора промптов (Normal/Maximized/Minimized) |
| `PromptBuilderWindowPlacementInitialized` | `false` | Флаг, что окно PromptBuilder уже было открыто и его геометрия инициализирована |
| `PromptBuilderLastMode` | `0` (Programming) | Последняя выбранная рубрика Конструктора промптов (PromptBuilderCategory enum) |
| `PromptBuilderPaintingStyle` | `Auto` | Последний выбранный стиль живописи |
| `PromptBuilderPaintingSection` | `All` | Последняя выбранная секция стилей живописи |
| `PromptBuilderPaintingArtist` | `Auto` | Последний выбранный художник для живописи |
| `PromptBuilderAnimationStyle` | `Auto` | Последний выбранный стиль анимации |
| `PromptBuilderAnimationSection` | `All` | Последняя секция стилей анимации |
| `PromptBuilderPhotoSection` | `All` | Последняя секция стилей фото |
| `PromptBuilderPhotoStyle` | `Auto` | Последний стиль фото |
| `PromptBuilderThemeSection` | `All` | Последняя секция стилей темы |
| `PromptBuilderThemeStyle` | `Auto` | Последний стиль темы |
| `PromptBuilderTextType` | `Auto` | Последний тип текстового промпта |
| `PromptBuilderTextTone` | `Neutral` | Последний тон текстового промпта |
| `PromptBuilderAnalysisDirection` | `Auto` | Последнее направление анализа |
| `PromptBuilderVideoDirection` | `Auto` | Последнее направление видео |
| `PromptBuilderProgrammingProjectType` | `Auto` | Последний тип проекта программирования |
| `PromptBuilderProgrammingStyle` | `Auto` | Последний стиль программирования |
| `PromptBuilderVisualTarget` | `Universal` | Последняя целевая модель для визуальных промптов |
| `PromptBuilderIconStyle` | `Auto` | Последний стиль иконок |
| `PromptBuilderGraphicType` | `Auto` | Последний тип графики |
| `PromptBuilderGraphicStyle` | `Auto` | Последний стиль графики |
| `PromptBuilderSelectedConnectionId` | — | Последнее выбранное AI-подключение в Prompt Builder |
| `PromptBuilderSelectedModelId` | — | Последняя выбранная AI-модель в Prompt Builder |
| `PromptBuilderSelectedProviderId` | — | Последний выбранный AI-провайдер в Prompt Builder |
| `PromptBuilderIsAutoModel` | `true` | Автовыбор модели в Prompt Builder для генерации промпта |
| `Ai` | объект | Настройки AI-подключений (список подключений, метаданные роутинга). См. раздел ниже. |
| `Sentry` | объект | Настройки телеметрии Sentry (Dsn, IsEnabled, Environment, TracesSampleRate, SendDefaultPii). Отключено по умолчанию. |
| `LastFileSortOperation` | — | Undo-состояние последней сессии File Sorter для отката нажатием на панели результата |
| `Contexts` | 10 панелей | Список панелей-контекстов `context-0`…`context-9`. Каждый PanelContext дополнительно содержит `IsNameCustomized`, `IconGlyph`, `Color` (AC9 — hex или RGB строка). |
| `ActiveContextId` | `context-0` | Активная панель |
| `Elements` | пустой список | Пользовательские кнопки |
| `UtilityButtonOrder` | пустой список | Порядок кнопок встроенных утилит |

**Nullable-геометрия окон.** Для полей `QuickNoteLeft/Top/Width/Height`, `TextProcessingLeft/Top/Width/Height`, `PromptBuilderLeft/Top/Width/Height`, `TaskbarIndicatorPositionX/Y` значение `—` означает `null` в JSON настроек. Пока пользователь ни разу не перемещал и не изменял размер окна, параметр не записан. При первом открытии:

- Окна утилит (Quick Note, Timer/Stopwatch, QR Code Generator, Clipboard Manager, Обработка текста, Дзен-редактор, Конструктор промптов и др.): размер и положение вычисляются через [UtilityWindowLayoutHelper.cs](../AiteBar/UtilityWindowLayoutHelper.cs): fallback к стандартному размеру (например, 680×520 для Quick Note), затем автоматически clamp-ится в рабочую область монитора.
- `TaskbarIndicatorPositionX/Y`: берётся середина текущего края (0,5 относительной координаты по оси панели).
- `TextProcessingWindowState`, `PromptBuilderWindowState`: Default = `Normal`.

После первого перемещения/resize окна или ручного перетаскивания индикатора параметр сохраняется в `settings.json` и в дальнейшем используется как точка старта.

## Настройки AI и хранилище учётных данных

Утилита **Обработка текста** использует внешние AI-провайдеры через HTTPS API. Конфигурация разделена на две части по соображениям безопасности:

| Часть | Где хранится | Назначение |
|---|---|---|
| Метаданные подключений (Id, ProviderId, DisplayName, ModelPreferences, IsSystem, Enabled и т. д.) | `%AppData%\Codebdbd\Aite Bar\settings.json`, секция `Ai` | Структура, порядок, включение подключений, предпочтения по моделям |
| Секреты (API-ключи, Secrets) | **Windows Credential Manager**, целевой префикс `AiteBar/AI/<connection-id>` | Секретные строки, никогда не записываются в `settings.json` |

Поддерживаемые провайдеры (каталог в `AiProviderCatalog`):

- **OpenRouter** (openrouter.ai) — агрегатор множества моделей; ключ OpenRouter
- **Cerebras** (cerebras.ai) — быстрые open-source модели; ключ Cerebras
- **Google Gemini** (generativelanguage.googleapis.com) — Gemini семейство; ключ Google AI Studio
- **Groq** (groq.com) — очень быстрые open-source модели; ключ Groq
- **GitHub Models** (models.inference.ai.azure.com) — платные GitHub-токены; для fine-grained Personal Access Token область `Models: read`, для классического GITHUB_TOKEN (Actions) scope `inference`
- **Mistral AI** (api.mistral.ai) — семейство Mistral; ключ Mistral

Сценарии выбора провайдеров/моделей (подробно см. [AI_PROVIDERS.md](AI_PROVIDERS.md)):

- Режим `auto` для подключения: выбирает самую дешёвую из подходящих по контексту моделей провайдера
- Список моделей подключения дедуплицируется по имени: если одинаковая модель встречается на нескольких провайдерах, она показывается один раз, а запросы роутятся по первому активному подключению с этой моделью
- У одного провайдера может быть несколько активных ключей — запросы циклически ротируются по ним без подмены выбранной модели

## Горячие клавиши

В UI доступны глобальные hotkey:

- показать или скрыть панель;
- следующая панель;
- предыдущая панель;
- добавить кнопку;
- запустить File Sorter;
- запустить Icon Converter;
- запустить Quick Note;
- запустить Color Picker;
- запустить Timer/Stopwatch;
- запустить QR Code Generator.
- запустить Clipboard Manager.
- запустить Обработку текста.
- запустить Дзен-редактор.
- запустить Конструктор промптов.

**Дефолтные комбинации на чистом профиле** (до первой ручной правки настроек):

| Hotkey | Поле настроек | Дефолт |
|---|---|---:|
| Показать или скрыть панель | `GlobalHotkeyCtrl / Alt / Shift / Win + Key` | `Alt + D4` |
| Следующая панель | `NextContextHotkey` | не назначено |
| Предыдущая панель | `PreviousContextHotkey` | не назначено |
| Добавить кнопку | `AddButtonHotkey` | не назначено |
| Запустить File Sorter | `FileSorterHotkey` | не назначено |
| Запустить Icon Converter | `IconConverterHotkey` | не назначено |
| Запустить Quick Note | `QuickNoteHotkey` | не назначено |
| Запустить Color Picker | `ColorPickerHotkey` | не назначено |
| Запустить Timer/Stopwatch | `TimerStopwatchHotkey` | не назначено |
| Запустить QR Code Generator | `QRCodeGeneratorHotkey` | не назначено |
| Запустить Clipboard Manager | `ClipboardManagerHotkey` | не назначено |
| Запустить Обработку текста | `TextProcessingHotkey` | не назначено |
| Запустить Дзен-редактор | `ZenEditorHotkey` | не назначено |
| Запустить Конструктор промптов | `PromptBuilderHotkey` | не назначено |

Все Hotkey-поля используют структуру `HotkeyBinding { Ctrl, Alt, Shift, Win, Key }`. Если пользователь не сохранял настройки, поле остаётся пустым `new();`, и hotkey не регистрируется в Windows.

Внутри окна настроек нельзя сохранить две одинаковые назначенные комбинации. Если Windows не даёт зарегистрировать hotkey, настройки сохраняются, но приложение показывает предупреждение, а комбинация не работает до изменения.

Поддерживаемые модификаторы:

- `Ctrl`;
- `Alt`;
- `Shift`;
- `Win`;
- распространённые комбинации этих модификаторов.

Поддерживаемые клавиши:

- `Space`;
- `[`;
- `]`;
- `A-Z`;
- `0-9`;
- `NumPad0-NumPad9` и операторы numpad;
- `F1-F12`.

## Константы анимаций

Длительности анимаций централизованы в `AiteBar/Constants.cs`, чтобы MainWindow, drag-and-drop и утилиты не расходились по значениям:

| Константа | Значение | Назначение |
|---|---:|---|
| `AnimationFadeMs` | `140` | Fade-in/fade-out кнопок при drag-and-drop |
| `AnimationSlideMs` | `150` | Slide-анимация перестановки кнопок |
| `PanelShowAnimationMs` | `175` | Показ панели |
| `PanelHideAnimationMs` | `140` | Скрытие панели |
| `QuickNoteSlideMs` | `200` | Анимация окна Quick Note |
| `PanelScreenPadding` | `20` | px-отступ панели от края экрана по осям X/Y при layout-расчётах (обеспечивает pixel-perfect границу без прижатия в пиксель к краю) |
| `DragHandleSpan` | `18` | Толщина drag handle в px, по которой пользователь цепляет панель мышью для перетаскивания на другой край |
| `WheelDeltaPerContextSwitch` | `120` | Значение `MouseWheelEventArgs.Delta`, после которого происходит одно переключение контекста колесом мыши над панелью |
| `ContextWheelSwitchCooldownMs` | `220` | Задержка в миллисекундах между переключениями контекстов колесом, чтобы избежать «дребезга» на высоких значениях delta |
| `ButtonOuterSize` | `44` | Базовая размерность ячейки (outer box) для квадратных кнопок панели — clickable area в px |
| `SeparatorSize` | `9` | Ширина/высота разделителя между кнопками (в зависимости от ориентации `Edge`) |
| `PanelChrome` | `8` | Толщина внешней рамки (chrome) панели — влияет на внутренний padding и позиционирование индикатора относительно `PanelPositionHelper` |

## Типы пользовательских действий

| Тип | Техническое поведение |
|---|---|
| Web | Запускает URL в выбранном браузере, профиле и режиме |
| Программа | Запускает `.exe`, `.lnk` или `.appref-ms` |
| Файл | Открывает файл через системную ассоциацию |
| Папка | Открывает папку в Проводнике |
| Скрипт | Запускает `.bat`, `.cmd`, `.ps1` или `.py` после подтверждения |
| Команда | Запускает команду через командную оболочку после подтверждения |
| Hotkey | Отправляет выбранное сочетание клавиш в активное окно |

Для `.py` при сохранении проверяется наличие `python.exe` в `PATH`.

## Команды и скрипты

Команды выполняются через командную оболочку. PowerShell-скрипты запускаются через доступный PowerShell. Скрипты и команды требуют подтверждения перед запуском.

Для команд действует дополнительное предупреждение, если строка содержит shell chaining или redirection (`&`, `|`, `>`, `<`) либо потенциально разрушительные команды (`del`, `erase`, `rd`, `rmdir`, `rm`, `remove-item`, `format`, `shutdown`, `restart-computer`, `stop-computer`, `bcdedit`, `diskpart`, `cipher`). Это предупреждение не блокирует запуск само по себе: пользователь всё равно принимает финальное решение в confirmation dialog.

Технические строки запуска, такие как `cmd.exe /c`, `powershell.exe -NoProfile`, `pwsh.exe` и детали политики выполнения PowerShell, должны оставаться в технической документации, а не в user manual.

## Браузеры и профили

Редактор web-кнопки показывает Chrome, Edge, Brave, Yandex и Firefox. Внутренняя модель и сервисы также могут учитывать Opera, Opera GX и Vivaldi, но эти варианты не являются основными пользовательскими пунктами текущего редактора.

Поддерживаемые режимы:

- обычный запуск;
- app mode для Chromium-браузеров;
- incognito/private;
- fullscreen после запуска;
- выбранный профиль;
- ротация профилей.

Если список профилей ротации пуст, это трактуется как "использовать все доступные профили".

## Встроенные инструменты

| Инструмент | Техническое поведение |
|---|---|
| Поиск | Ищет текст из буфера обмена в Google |
| Скриншот | Открывает `ms-screenclip:` |
| Видео | Открывает `ms-screenclip:?type=recording` |
| Калькулятор | Запускает `calc.exe` |
| Проводник | Запускает `explorer.exe` |
| Загрузки | Открывает `shell:Downloads` |
| Таймер и секундомер | Открывает встроенное окно таймера/секундомера |
| Выбор цвета | Запускает overlay-пипетку и копирует HEX |
| Quick Note | Открывает локальную заметку с автосохранением |
| File Sorter | Утилита для сортировки файлов по расширению и другим правилам |
| Icon Converter | Утилита для конвертации изображений в формат ICO |
| QR Code Generator | Утилита для создания QR-кодов с экспортом в PNG/SVG |
| Clipboard Manager | Показывает runtime-историю текста и изображений из буфера обмена |
| Show Desktop | Минимизирует окна и показывает рабочий стол |
| Apps Folder | Открывает системную папку Applications |
| Copilot | Запускает Windows Copilot |
| Обработка текста (AI) | AI-окно с потоковой SSE-генерацией, диффом оригинала/результата, Undo/Redo, отмена потока, 4 preset-режима Proofread/Typography/Cleanup/LiteraryEdit |
| Дзен-редактор (AI) | Полноэкранный редактор Markdown с темами, множеством документов, поиском по документам, экспортом TXT и корзиной недавно удалённых |
| Конструктор промптов (AI) | Окно с 11 рубриками (Программирование, Изображения, Тексты, Видео, Анализ, Музыка, Идеи, Живопись, Анимация, Иконки, Графика), пресетами стилей, предпросмотром и AI-генерацией готового промпта |

## Таймер и секундомер

Окно таймера/секундомера открывается из встроенных быстрых инструментов. Поддерживаются:

- режим таймера с пресетами от 1 до 120 минут;
- ввод своего времени в формате `hh:mm:ss`, `mm:ss` или минуты одним числом;
- старт, пауза и сброс;
- режим секундомера с отображением сотых долей секунды;
- компактный режим окна;
- звук окончания таймера, который можно отключить;
- сохранение последнего режима, длительности и настройки звука.

## Quick Note

Quick Note сохраняет данные в `QuickNote.md`. Поддерживаются:

- автосохранение;
- базовое форматирование;
- открытие файла во внешнем редакторе;
- очистка заметки с подтверждением;
- темы;
- изменение размера и положения окна;
- закрытие по `Esc` и по клику вне окна;
- `Ctrl+Shift+C` для копирования всего текста;
- `Ctrl+клик` для открытия URL.

Для больших заметок подсветка ссылок может отключаться из соображений производительности.

## Импорт и экспорт `.aitebarpanel`

Экспорт сохраняет текущую панель в пакет `.aitebarpanel`.

В пакет входят:

- manifest с описанием панели;
- пользовательские кнопки активной панели;
- связанные пользовательские и импортированные иконки, если они нужны кнопкам.

Импорт:

- проверяет пакет;
- проверяет manifest;
- копирует импортированные иконки в локальное хранилище;
- добавляет кнопки в текущую панель;
- не меняет имя текущей панели.

Ограничения размера пакета и проверки manifest относятся к технической валидации и не должны подробно описываться в user manual.

## Резервное копирование `.aitebarbackup`

Формат бэкапа: ZIP-архив с расширением `.aitebarbackup`, максимальный размер 100 МБ. Генерируется и читается через [BackupService.cs](../AiteBar/BackupService.cs).

### Состав архива

| Entry в ZIP | Обязательно | Шифрование | Назначение |
|---|---:|---|---|
| `settings.json` | да | нет | Полная сериализация `AppSettings` (10 панелей-контекстов, все hotkeys, ShowPreset*, геометрии окон, AI-подключения без секретов, порядок кнопок). |
| `icons/<relative-path>` | нет | нет | Все пользовательские иконки из `%AppData%\Codebdbd\Aite Bar\icons`. Набор записей зависит от наличия пользовательских иконок на панели. |
| `private.bin` | нет | **AES-GCM-256 + PBKDF2** | Только при `IncludeSecrets=true`. Контейнер с: 1) API-ключами AI-подключений из Credential Manager (`BackupSecretPayload.Credentials`), 2) опционально снимком истории буфера обмена (`ClipboardEntries`). |

### Параметры экспорта

Опции задаются в [BackupPasswordDialog.xaml](../AiteBar/BackupPasswordDialog.xaml) и передаются как `BackupCreateOptions`:

| Опция | Тип | Описание |
|---|---|---|
| `IncludeSecrets` | bool | Включать ли `private.bin`. Если `true` — `Password` обязателен. |
| `IncludeClipboard` | bool | Только при `IncludeSecrets=true`. Сохранить текущий `ClipboardHistoryService.Entries` снимок в `private.bin`. |
| `Password` | string? | Пароль пользователя для ключа PBKDF2. |

### Шифрование private.bin

Параметры криптографии (жёстко закодированы в [BackupService.cs](../AiteBar/BackupService.cs)):

| Параметр | Значение |
|---|---:|
| Алгоритм | `AesGcm` (256-bit key, 128-bit tag) |
| Размер ключа | 32 байта (256 bit) |
| Key derivation | `Rfc2898DeriveBytes.Pbkdf2` |
| Hash | `SHA256` |
| Итераций PBKDF2 | `210_000` |
| Salt | 16 случайных байт |
| Nonce/IV | 12 случайных байт |
| Tag (MAC) | 16 байт |

Формат `private.bin` на диске: `[salt 16B][nonce 12B][tag 16B][ciphertext N bytes]`. После дешифрации payload парсится как UTF-8 JSON с `BackupSecretPayload`. Неверный пароль → `CryptographicException`, который маппится в `InvalidDataException("Backup password is incorrect or data is damaged.")`.

### Восстановление

`RestoreAsync()` выполняет шаги строго в порядке:

1. **Safety backup**: снимает текущее состояние перед заменой (в случае неудачного восстановления).
2. **Read archive**: вызывает `ReadAsync()` и валидирует структуру, размеры и пути иконок (защита от path traversal `..`).
3. **Write icons**: записывает `icons/` в `PathHelper.IconsFolder` с проверкой `destination.StartsWith(root)` для недопущения выхода за границы.
4. **Write secrets**: при наличии decrypted `BackupSecretPayload`:
   - записывает каждый Credential из `Credentials` в `IAiCredentialStore.Write(target, secret)`;
   - вызывает `ClipboardHistoryService.Instance.ReplaceEntries(ClipboardEntries)`.
5. **Apply settings**: `settingsService.Settings = backup.Settings` → `NormalizeAppState()` → `SaveAsync()`.

### UI точки входа

Кнопки в разделе `Backups` вкладки `About` [AppSettingsWindow.xaml#L1110-L1116](../AiteBar/AppSettingsWindow.xaml#L1110-L1116):

| Действие | Метод-обработчик |
|---|---|
| Create backup... | `AppSettingsWindow.xaml.cs: BtnCreateBackup_Click()` |
| Restore backup... | `AppSettingsWindow.xaml.cs: BtnRestoreBackup_Click()` |

## Контекстные меню

Главная панель:

- список включенных панелей;
- `Импорт в текущую панель...`;
- `Экспорт текущей панели...`.

Пользовательская кнопка:

- `Редактировать`;
- `Дублировать`;
- `Переименовать`;
- `Переместить`;
- `Копировать URL`;
- `Копировать путь`;
- `Копировать команду`;
- `Открыть расположение`;
- `Удалить`.

Встроенный инструмент:

- `Открепить`.

Tray-меню:

- `Открыть`;
- `Настройки программы`;
- `Проверить обновления`;
- `О программе`;
- `Поддержать автора`;
- `Закрыть и выйти`.

## Сборка и проверка

Сборка Release:

```powershell
dotnet build .\AiteBar.sln -c Release
```

Тесты:

```powershell
dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
```

Fallback для WPF/MSBuild temp-проблем:

```powershell
dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll
```

Сборка инсталлятора:

```powershell
.\installer\Build-Installer.ps1
```

Publish-артефакты:

```text
artifacts\publish\win-x64
```

Installer-артефакты:

```text
artifacts\installer
```
