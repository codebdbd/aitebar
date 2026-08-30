# Changelog

🇷🇺 Все значительные изменения проекта будут документироваться в этом файле.

🇬🇧 All notable changes to this project will be documented in this file.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/),
проект придерживается [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.15.20] - 2026-08-31

### 🇷🇺 Исправлено | 🇬🇧 Fixed

- **Стабилизация релиза**: устранены гонка сохранения Clipboard History и пропуск cleanup при закрытии; укреплены multi-monitor hover, no-activate показ, Sentry startup и update-check timeout.
- **Release stabilization**: fixed the Clipboard History persistence race and skipped shutdown cleanup; hardened multi-monitor hover, no-activate panel show, Sentry startup, and the update-check timeout.

## [1.15.19] - 2026-08-29

### 🇷🇺 Исправлено | 🇬🇧 Fixed

- **Быстрая заметка — стабильность редактирования**: все изменения документа при загрузке, очистке, форматировании и применении темы выполняются внутри WPF change-групп. Сериализация остаётся на UI-потоке, атомарная запись — в фоновом потоке.
- **Быстрая заметка — выпускная проверка**: добавлены регрессии для 700 мс autosave, единого Undo форматирования, лимитов изображений, пяти conflict-копий и нагрузки 1000 абзацев с 20 изображениями.
- **Quick Note editing stability**: load, clear, formatting and theme mutations now execute inside WPF change groups. Serialization remains on the UI thread and atomic file writing stays off it.
- **Quick Note release verification**: added regressions for the 700 ms autosave, single-step formatting Undo, image limits, five conflict copies and a 1000-paragraph/20-image workload.

## [1.15.18] - 2026-08-29

### 🇷🇺 Исправлено | 🇬🇧 Fixed

- **Быстрая заметка — пустой редактор**: полностью удалена подсказка «Вставьте текст, ссылки, команды…» и связанная с ней логика видимости. Пустая заметка открывается как чистое поле ввода.
- **Quick Note empty editor**: removed the “Paste text, links, commands…” placeholder and its visibility plumbing. An empty note is now a clean editing surface.

## [1.15.17] - 2026-08-29

### 🇷🇺 Исправлено | 🇬🇧 Fixed

- **Быстрая заметка — редактирование после вставки**: обычный клик по автоматически распознанному URL, e-mail или номеру телефона теперь ставит каретку и не запускает системное окно выбора приложения. Для открытия ссылки используется `Ctrl+клик`. Тонкий скролл у правого края сохранён без замены внутреннего текстового контейнера.
- **Quick Note editing after paste**: a normal click on an automatically recognized URL, e-mail address or phone number now places the caret instead of launching the shell application chooser. Links open with `Ctrl+click`. The compact right-edge scrollbar remains without replacing WPF's internal text host.

## [1.15.16] - 2026-08-28

### 🇷🇺 Исправлено | 🇬🇧 Fixed

- **Быстрая заметка — прокрутка**: тонкий ползунок у правого края вместо широкой полосы с лишним отступом. Видимая ширина — 2 единицы, область захвата — 6; при наведении ползунок расширяется без сдвига текста. Сохранены прокрутка колёсиком, перетаскивание и доступ к краю изменения размера окна.
- **Quick Note scrollbar**: a slim indicator near the right edge with a compact six-unit input track. Hover/drag expansion does not move the text; wheel scrolling, thumb dragging and native resize grips remain available.

## [1.15.15] - 2026-08-28

### 🇷🇺 Исправлено | 🇬🇧 Fixed

- **Быстрая заметка — захват края**: расширена внутренняя боковая зона изменения размера. Можно захватить край самой неактивной заметки без предварительного клика в текст; операцией по-прежнему управляет Windows Snap. Кнопки и полоса прокрутки сохраняют приоритет обработки нажатий. Системная плашка при изменении размера со стороны другого приложения не отключается.
- **Quick Note edge capture**: widened the inner side resize grip so the note can initiate native shared resizing without a preliminary content click. Toolbar and scrollbar hit targets remain interactive. This does not disable Windows' preview when another application initiates the resize.

## [1.15.14] - 2026-08-18

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Быстрые заметки (производительность)**: полностью переписан алгоритм `GetTextPointerAtOffset` на бинарный поиск по листовым inline-элементам вместо посимвольного O(N^2) обхода с постоянной сериализацией всего текста документа, что полностью устранило зависания при очистке форматирования.
- **QuickNote performance**: fully refactored `GetTextPointerAtOffset` algorithm to use binary search over leaf inline elements instead of O(N^2) character-by-character symbol collection and full-document string serializations, completely eliminating lag/hangs when clearing formatting.
- **Перетаскивание контекстов панелей**: обновлен визуал перетаскивания в окне настроек — добавлен эффект ghost-прозрачности (0.35) для перетаскиваемой строки, убрано лишнее двойное выделение рамкой цели вставки, а индикатор вставки заменен на аккуратную линию с точкой-указателем слева.
- **Panel context drag-and-drop**: redesigned drag-and-drop visuals in settings — added a ghost opacity effect (0.35) for the dragged row, removed redundant highlight borders on the target row, and replaced insertion indicators with a clean line and dot pointer on the left.

## [1.15.13] - 2026-08-17

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Быстрые заметки (форматирование)**: убрано двойное планирование сохранения при очистке форматирования, применении списков и заголовков, которое вызывало мгновенный статус «Сохранение» и подвисание интерфейса.
- **QuickNote formatting save scheduling**: removed double save scheduling when clearing formatting, applying lists, or headings that caused an immediate “Saving” status and UI freezes.
- **Менеджер профилей / диалог быстрой ссылки**: контекстное меню полей ввода теперь тёмное (как в других утилитах); локализован заголовок окна в RU/UK/DE; убрано красное предупреждение о валидации — состояние через `IsEnabled` кнопки Сохранить.
- **AiteProfiles / Quick link dialog**: input context menus now use the dark style (consistent with other utilities); window title localized in RU/UK/DE; removed the red validation warning — state is communicated via the Save button `IsEnabled`.
- **Глобальное контекстное меню `DarkContextMenu`**: добавлен контрастный 1px бордер `#46464F` для разделения с панелью при одинаковом тёмном фоне; фон `#2C2C2C` и скругления углов не изменены.
- **Global `DarkContextMenu`**: added a contrasting 1px `#46464F` border to separate it from panels on the same dark background; `#2C2C2C` background and corner radius are unchanged.

## [1.15.12] - 2026-08-15

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Глифы утилит**: исправлены глифы для FileSorterWindow (удаление, открыть папку, откатить), ZenEditor, FileSorter, PromptBuilder и AiteProfiles для соответствия шрифту FluentSystemIcons-Regular.
- **Utility glyphs**: fixed glyphs for FileSorterWindow (delete, open folder, undo), ZenEditor, FileSorter, PromptBuilder, and AiteProfiles to match the FluentSystemIcons-Regular font.
- **Цвета кнопок AiteProfiles**: унифицированы цвета кнопок — закрепить и ротация используют один цвет (#FFD166), остальные кнопки — другой (#5EEAD4).
- **AiteProfiles button colors**: unified button colors — pin and rotation use the same color (#FFD166), other buttons use a different color (#5EEAD4).
- **Фон утилит**: унифицирован фон всех утилит на #191919 через ресурс PanelBackground.
- **Utility background**: unified background across all utilities to #191919 via the PanelBackground resource.
- **Удаление документов в Zen Editor**: добавлена кнопка удаления документа прямо в строку списка документов с глифом U+F34D; кнопка скрыта для текущего документа.
- **Zen Editor document deletion**: added delete button directly in the document list row with glyph U+F34D; button is hidden for the current document.
- **Кнопка удаления папки в FileSorter**: добавлена кнопка удаления сохранённой папки в строку списка (глиф U+F34D); скрыта для системных папок (Desktop, Downloads); IsEnabled синхронизирован с состоянием busy.
- **FileSorter remove folder button**: added saved-folder removal button in the list row (glyph U+F34D); hidden for system folders (Desktop, Downloads); IsEnabled syncs with the busy state.
- **Автокопирование в Zen Editor**: убрано автоматическое копирование выделения в буфер обмена (Ctrl+A и обычное выделение больше не затирают буфер пользователя); копирование только явно через Ctrl+C или меню.
- **Zen Editor auto-copy on selection**: removed automatic selection-to-clipboard copy (Ctrl+A and regular selection no longer overwrite the user's clipboard); copying is now explicit via Ctrl+C or the menu only.
- **Синхронизация версии сборки**: исправлена рассинхронизация версии между AiteBar.csproj (1.15.12), AssemblyInfo.cs и installer/AiteBar.iss, которая вызывала FileNotFoundException при старте; добавлен тест ReleaseVersionTests для контроля синхронизации.
- **Assembly version sync**: fixed version mismatch between AiteBar.csproj (1.15.12), AssemblyInfo.cs, and installer/AiteBar.iss that caused a startup FileNotFoundException; ReleaseVersionTests test added to guard the sync.
- **Релизные тесты**: актуализированы ожидания глифов FileSorterWindow и ZenEditor под FluentSystemIcons-Regular.
- **Release tests**: updated FileSorterWindow and ZenEditor glyph expectations for FluentSystemIcons-Regular.

## [1.15.11] - 2026-08-12

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Литературная редакция текста**: вкладки обработки текста снова используют собственные самостоятельные инструкции без общего поведенческого хвоста; литературная редакция теперь может реально переписывать неудачные фразы, сохраняя язык, смысл и факты.
- **Literary text editing**: text-processing tabs now use their own self-contained instructions without a shared behavioral suffix; literary editing can actually rewrite awkward phrasing while preserving language, meaning, and facts.
- **Перетаскивание панелей**: в настройках «Имена панелей» добавлены заметная ручка, подсветка перетаскиваемой строки и синяя линия вставки выше/ниже цели, чтобы было понятно, что и куда переносится.
- **Panel drag and drop**: the “Panel names” settings list now shows a clearer handle, highlights the dragged row, and displays a blue insertion line above or below the target row so the drop position is explicit.

## [1.15.10] - 2026-08-11

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Импорт AiteCommander**: ZIP-архивы AiteCommander импортируются в текущую панель вместе со ссылками, типами действий и доступными пользовательскими иконками; общий фильтр файлов сразу показывает `.aitebarpanel` и `.zip`.
- **AiteCommander import**: AiteCommander ZIP archives can be imported into the current panel with links, action types, and available custom icons; the default file filter now shows both `.aitebarpanel` and `.zip`.
- **Управление панелями**: панели можно переставлять перетаскиванием в настройках; порядок используется переключением, индикатором и прямыми горячими клавишами.
- **Panel management**: panels can be reordered by drag and drop in settings; switching, the active indicator, and direct hotkeys follow the saved order.
- **Пользовательские иконки**: выбор иконки кнопки поддерживает безопасный импорт SVG с преобразованием в PNG; существующая поддержка ICO сохранена.
- **Custom icons**: button icon selection supports safe SVG import converted to PNG while retaining existing ICO support.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Очистка панели**: подтверждённая очистка удаляет ссылки выбранной панели из фактического состояния приложения и сохраняет результат, не затрагивая другие панели.
- **Panel clearing**: confirmed clearing removes links from the selected panel in the persisted application state without affecting other panels.
- **Релизные тесты**: проверка первоначального выбора модели больше не зависит от формата переносов строк `LF`/`CRLF`.
- **Release tests**: the initial model-selection check no longer depends on `LF` versus `CRLF` source line endings.

## [1.15.9] - 2026-08-09

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Конструктор промптов**: каждый новый запрос повторно обращается к провайдеру после любого отказа модели; сохранённые runtime-статусы `401`, `402`, `403` и `429` больше не блокируют генерацию внутри запущенного приложения.
- **Prompt Builder**: every new submission reaches the provider again after any model failure; cached `401`, `402`, `403`, and `429` runtime states no longer block generation inside the running application.
- **Генератор**: выбранная модель корректно отображается общим шаблоном ComboBox и поддерживает бегущую строку для длинного названия.
- **Generator**: the selected model is rendered correctly by the shared ComboBox template and retains marquee behavior for long names.

## [1.15.8] - 2026-08-09

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Конструктор промптов**: выбранный генератор корректно отображается в ComboBox, включая значение «Автоматически».
- **Prompt Builder**: the selected generator is displayed correctly in the ComboBox, including “Automatically”.

## [1.15.7] - 2026-08-09

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Конструктор промптов**: «Автоматически» отображается в генераторе сразу при открытии окна; каталог моделей кэшируется между повторными открытиями утилиты.
- **Prompt Builder**: “Automatically” is displayed in the generator immediately when the window opens; the model catalog is cached between subsequent openings of the utility.

## [1.15.6] - 2026-08-09

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Вертикальная панель**: «Контекст» и «Добавить» остаются в двух колонках, если их вертикальное расположение вызвало бы перенос пользовательской кнопки.
- **Vertical panel**: Context and Add stay in two columns when stacking them would wrap a user button.
- **Конструктор промптов**: ручная замена текста после генерации всегда становится новым запросом и больше не подставляет удалённый предыдущий запрос.
- **Prompt Builder**: manually replacing text after generation always creates a new request and no longer restores the removed previous request.

## [1.15.5] - 2026-08-09

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Конструктор промптов**: повторная генерация после временного ответа `403 Forbidden` снова обращается к провайдеру, а не остаётся заблокированной внутренним статусом приложения.
- **Prompt Builder**: retrying after a transient `403 Forbidden` now reaches the provider again instead of remaining blocked by the application's internal status.

## [1.15.4] - 2026-08-09

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Конструктор промптов**: каталог кода приведён к понятной схеме «Тип + Стиль», а неактуальные параметры удалены из активной логики и настроек.
- **Prompt Builder**: the Code catalog now follows a clear “Type + Style” scheme, and obsolete parameters were removed from active logic and settings.
- **Локализация**: синхронизированы каталоги и переводы Prompt Builder на русском, украинском, немецком и английском; проверка языковых ресурсов снова проходит полностью.
- **Localization**: Prompt Builder catalogs and translations are synchronized across Russian, Ukrainian, German, and English; resource validation now passes completely.
- **Стабильность**: устранено зависание при обновлении интерфейса после смены языка; полный набор автоматических тестов завершается.
- **Stability**: fixed a UI refresh deadlock after a language change; the full automated test suite completes.

## [1.15.3] - 2026-08-09

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Конструктор промптов**: унифицированы фильтры в визуальных вкладках, обновлены каталоги стилей и сюжетов, исправлено отображение длинных значений в комбобоксах без поломки списков моделей.
- **Prompt Builder**: unified filters across visual tabs, refreshed style and scenario catalogs, and fixed long combo-box value rendering without breaking model lists.

## [1.15.2] - 2026-08-08

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Release validation**: тесты Prompt Builder теперь одинаково проверяют многострочные шаблоны на Windows и в CI.
- **Release validation**: Prompt Builder tests now validate multiline templates consistently on Windows and CI.

## [1.15.1] - 2026-08-08

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Конструктор промптов — «Графика»**: автоматический выбор модели больше не отклоняет запрос по устаревшему локальному списку; gateway выбирает доступный маршрут и fallback.
- **Prompt Builder — Graphics**: automatic model selection no longer rejects requests using a stale local list; the gateway selects an available route and fallback.
- **Конструктор промптов**: очистка и создание нового варианта выполняются сразу, без лишних окон подтверждения.
- **Prompt Builder**: clearing and creating a new variant now run immediately, without unnecessary confirmation dialogs.
- **Контекстные меню**: глифы во всех меню теперь центрируются по видимому контуру, а не по baseline шрифта.
- **Context menus**: glyphs in every menu are now centered by their visible outline instead of the font baseline.

## [1.15.0] - 2026-08-08

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Панели 0–9 и быстрое переключение**: AiteBar поддерживает 10 контекстных панелей с яркой контрастной палитрой. `Alt+0`…`Alt+9` открывает панель и выбирает соответствующий включённый контекст; номер текущей панели переключает контексты кликом, `Enter`, `Shift` + клик или `Shift+Enter`.
- **Panels 0–9 and fast switching**: AiteBar now supports 10 context panels with a bright, high-contrast palette. `Alt+0`…`Alt+9` opens the panel and selects the matching enabled context; the active-panel badge switches contexts with click, `Enter`, `Shift`+click, or `Shift+Enter`.
- **Конструктор промптов**: Добавлены отдельные вкладки «Иконки» и «Графика» с независимыми шаблонами, параметрами и черновиками; вкладка «Код» теперь показывает краткий результат выбранного типа задачи.
- **Prompt Builder**: Added separate Icons and Graphics tabs with independent templates, options, and drafts; the Code tab now shows a concise outcome for the selected task type.
- **Резервные копии**: Добавлен экспорт и восстановление настроек и локальных данных приложения.
- **Backups**: Added export and restore for application settings and local data.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Локализация Prompt Builder**: Полные переводы вкладок, стилей и типов ресурсов доступны на русском, украинском, немецком и английском языках.
- **Prompt Builder localization**: Complete translations for tabs, styles, and asset types are available in Russian, Ukrainian, German, and English.
- **Тесты локализации**: Устранено зависание проверки локализации между потоками; полный набор тестов снова может завершаться.
- **Localization tests**: Removed the cross-thread localization test deadlock so the full test suite can complete.

## [1.14.0] - 2026-08-02

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Утилита Prompt Builder**: Новое окно конструирования промптов с шаблонами ролей, переменными, предпросмотром, историей и интеграцией с AiGateway; отдельные юнит- и интеграционные тесты и ExecPlan.
- **Prompt Builder utility**: New prompt-construction window with role templates, variables, preview, history, and AiGateway integration; dedicated unit/integration tests and ExecPlan.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Обработка текста — расширенные режимы**: Дополнены режимы работы сервиса, синхронизирована разметка окна и code-behind; расширены контрактные тесты сервиса, визуального слоя и layout.
- **Text Processing extended modes**: Additional service modes, synchronized window markup and code-behind; expanded service, visual, and layout contract tests.
- **Документация и локализация**: Обновлены `functions.md`, `USER_MANUAL.md`, `README.md`, строковые ресурсы Strings.ru/uk/de.resx для Prompt Builder и изменений TextProcessing.
- **Docs and localization**: Updated `functions.md`, `USER_MANUAL.md`, `README.md`, and Strings.ru/uk/de.resx resources for Prompt Builder and TextProcessing changes.

## [1.13.0] - 2026-08-02

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Литературная редакция текста**: В «Обработку текста» добавлен отдельный режим бережной литературной правки с собственным профессиональным промптом, настройками модели и контролем сохранения языка и содержания.
- **Literary text editing**: Text Processing now includes a dedicated conservative literary-editing mode with its own professional prompt, model settings, and language/content preservation checks.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Управление появлением панели**: В общие настройки перед указателем положения добавлен переключатель показа панели при наведении мыши; hotkey, tray и указатель продолжают открывать панель независимо от него.
- **Panel hover activation control**: General settings now include a mouse-hover activation switch immediately before the position-indicator option; hotkey, tray, and indicator opening remain available independently.
- **Сортировка файлов — единый экран**: Рабочий стол, Загрузки и пользовательские папки собраны в одном списке с переключателем, прогрессом, результатом, откатом и открытием папки в каждой строке; основные кнопки используют единый стиль и размер.
- **File Sorter single-screen workflow**: Desktop, Downloads, and custom folders now share one list with per-row selection, progress, result, undo, and open-folder actions; the primary buttons use the same size and style.
- **Обработка текста — профессиональные инструкции**: Независимые промпты проверки, типографики, очистки и литературной правки приведены к краткому английскому формату; список моделей отфильтрован по пригодности для текстовой обработки.
- **Text Processing professional instructions**: Independent proofreading, typography, cleanup, and literary-editing prompts now use concise professional English instructions, and the model list is filtered for text-processing suitability.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Главная панель — подсказки и клавиатура**: Номер активного контекста снова показывает подсказку с именем, а перемещение Tab и стрелками снова отображает синюю обводку сфокусированной кнопки.
- **Main panel hints and keyboard focus**: The active context number again shows a tooltip with its name, and Tab/arrow navigation again draws a blue outline around the focused button.
- **Обработка текста — предсказуемый результат**: Повтор всегда использует исходный текст и выбранный режим; добавлены защита от смены языка, пустого потокового ответа и технических фрагментов, а первой вкладкой снова открывается проверка текста.
- **Text Processing predictable output**: Retry always uses the original text and selected mode; safeguards now reject language changes, empty streaming responses, and modified technical fragments, while Proofreading is again the startup tab.
- **Системные утилиты — ошибки запуска**: Снимок экрана, запись, калькулятор, проводник, загрузки, показ рабочего стола и папка приложений больше не скрывают отказ Windows запустить действие; Copilot гарантированно освобождает Win-клавишу при сбое.
- **System utility launch failures**: Screenshot, recording, Calculator, Explorer, Downloads, Show Desktop, and Apps Folder no longer hide Windows launch failures; Copilot reliably releases the Win key after an injection error.
- **Надёжность и локализация утилит**: Color Picker использует локализованное аварийное сообщение, Timer/Stopwatch безопасно обрабатывает ошибку сохранения при закрытии, а новые WPF-тесты File Sorter изолированы в release pipeline.
- **Utility reliability and localization**: Color Picker now uses a localized failure dialog, Timer/Stopwatch safely contains close-time save failures, and the new File Sorter WPF tests run in isolated release hosts.

## [1.12.2] - 2026-07-31

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Глобальные hotkey новых утилит**: В настройках горячих клавиш добавлены отдельные назначаемые комбинации для «Обработки текста» и «Дзен-редактора» с общей проверкой конфликтов и запуском через Win32 `RegisterHotKey`.
- **Global hotkeys for new utilities**: Hotkey settings now include assignable shortcuts for Text Processing and Zen Editor, with shared conflict validation and launch through Win32 `RegisterHotKey`.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Дзен-редактор — плавный набор текста**: Убран полный обход форматирования и повторный расчёт позиции каретки после каждого символа; заголовок документа больше не копирует всё содержимое ради первой строки. Инкрементальное обновление сохраняет жирность, курсив и подчёркивание при вводе.
- **Zen Editor typing performance**: Removed the full formatting scan and repeated caret-position traversal on every keystroke; document-title calculation no longer copies the whole body to read its first line. Incremental updates preserve bold, italic, and underline while typing.

### 🇷🇺 Документация | 🇬🇧 Documentation
- Актуализированы архитектурная справка, карта функций, руководство пользователя, сведения о конфиденциальности и поддерживаемых версиях.
- Updated the architecture reference, function map, user manual, privacy notes, and supported-version information.

## [1.12.1] - 2026-07-30

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Компактный таймер — действия кнопок**: Запуск/пауза и разворачивание используют отдельные обработчики и встроенные Fluent-глифы; добавлен поведенческий WPF-тест обеих команд.
- **Compact timer button actions**: Start/pause and expand now use dedicated handlers and bundled Fluent glyphs, with a behavioral WPF test covering both commands.
- **Дзен-редактор — клавиатура и окна**: `Shift+↑/↓` снова выполняет стандартное выделение строк, темы циклически переключаются через `Ctrl+Alt+↑/↓`, а неактивный полноэкранный редактор больше не перекрывает приложения после `Alt+Tab`.
- **Zen Editor keyboard and windows**: `Shift+Up/Down` once again performs standard line selection, themes cycle through `Ctrl+Alt+Up/Down`, and an inactive full-screen editor no longer covers applications after Alt+Tab.
- **Дзен-редактор — поиск и восстановление**: Добавлены временный поиск `Ctrl+F`, видимые команды форматирования, восстановление недавно удалённых документов и безопасная обработка ошибок асинхронных команд.
- **Zen Editor search and recovery**: Added temporary `Ctrl+F` search, discoverable formatting commands, recently deleted document restoration, and guarded asynchronous command failures.

## [1.12.0] - 2026-07-30

### 🇷🇺 Добавлено | 🇬🇧 Added
- **AI-подключения**: В настройках добавлено безопасное управление несколькими подключениями Cerebras, Google Gemini, Groq и Mistral AI с хранением ключей через Windows Credential Manager, проверкой соединения и выбором предпочтительной модели.
- **AI connections**: Settings now provide secure management of multiple Cerebras, Google Gemini, Groq, and Mistral AI connections with Windows Credential Manager storage, connection checks, and preferred-model selection.
- **Дзен-редактор**: Добавлена полноэкранная утилита для сосредоточенного письма с пятью локальными темами и встроенными шрифтами, несколькими внутренними документами, автосохранением, восстановлением, Undo/Redo и экспортом в TXT.
- **Zen Editor**: Added a full-screen focused-writing utility with five offline themes and bundled fonts, multiple internal documents, auto-save, recovery, Undo/Redo, and TXT export.
- **Обработка текста**: Добавлена AI-утилита для проверки орфографии, типографики и очистки артефактов копирования с выбором подходящей бесплатной модели и повтором последней обработки.
- **Text processing**: Added an AI utility for proofreading, typography, and copied-text cleanup with eligible free-model selection and repeat processing.
- **Обработка текста — контроль результата**: Добавлены потоковый вывод ответа, просмотр точных изменений, Undo/Redo через `Ctrl+Z`/`Ctrl+Y` и видимый таймер выполнения.
- **Text processing result control**: Added streamed responses, an exact changes view, `Ctrl+Z`/`Ctrl+Y` undo and redo, and a visible processing timer.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Дзен-редактор — типографика и производительность**: Добавлен фиксированный межабзацный интервал без изменения plain-text содержимого; обычный ввод, позиционирование курсора, автосохранение и список документов оптимизированы для больших текстов.
- **Zen Editor typography and performance**: Added fixed visual paragraph spacing without changing plain-text content; ordinary typing, caret positioning, auto-save, and document listing are optimized for large texts.
- **Дзен-редактор — экспорт TXT**: При экспорте между соседними абзацами добавляется одна пустая строка без удвоения уже существующего межабзацного интервала.
- **Zen Editor TXT export**: TXT export adds one blank line between adjacent paragraphs without duplicating an existing paragraph gap.
- **Дзен-редактор — форматирование**: Жирность, курсив и подчёркивание, применённые встроенными командами редактора, сохраняются во внутренних документах и восстанавливаются при следующем открытии; TXT остаётся обычным текстом.
- **Zen Editor formatting**: Bold, italic, and underline applied through the editor's built-in commands now persist in internal documents and are restored on reopen; TXT remains plain text.
- **Контекстные меню**: Программно создаваемые меню панели, tray, индикатора и Дзен-редактора используют единую фабрику геометрии, глифов и вертикального выравнивания.
- **Context menus**: Programmatically created panel, tray, indicator, and Zen Editor menus now share one factory for geometry, glyphs, and vertical alignment.
- **Интерфейс и архитектура**: Унифицированы командные кнопки и элементы форм, централизован каталог встроенных утилит, снижены аллокации при обновлении панели и улучшена изоляция асинхронных UI-операций.
- **UI and architecture**: Command buttons and form controls are unified, built-in utility metadata is centralized, panel refresh allocations are reduced, and asynchronous UI operations are better isolated.
- **Дзен-редактор — темы с клавиатуры**: `Shift+↑` и `Shift+↓` циклически переключают пять тем назад и вперёд.
- **Zen Editor keyboard themes**: `Shift+↑` and `Shift+↓` cycle backward and forward through all five themes.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Обработка текста — release UI**: Добавлены адаптивная компоновка, рабочая отмена, доступные состояния клавиатурного фокуса, сохранение текста сверх лимита без обрезания, восстановление окна и понятные состояния моделей, ошибок и буфера обмена.
- **Text processing release UI**: Added responsive layout, working cancellation, accessible keyboard focus, non-destructive over-limit handling, window restoration, and clear model, error, and clipboard states.
- **Обработка текста — надёжность**: Выбор ограничен бесплатными моделями, одинаковая модель показывается один раз для всех API-ключей провайдера, а шлюз ротирует эти ключи без скрытой подмены выбранной модели. Ответ сохраняется без эвристического удаления содержимого; вставка, повтор, обновление моделей, минимизация и восстановление режима приведены к стандартному поведению.
- **Text processing reliability**: Selection is restricted to free models, the same provider model appears once across all API keys, and the gateway rotates those keys without silently substituting the selected model. Responses are preserved without heuristic content removal; paste, repeat, model refresh, minimization, and mode restoration now follow standard behavior.
- **Обработка текста — модели и технические фрагменты**: Из списка исключены генераторы изображений и видео (Nano Banana, Imagen, Veo и аналоги); URL, e-mail, пути, код, теги, версии и идентификаторы защищаются от изменения моделью. Оценка контекста учитывает кириллицу, но не выводится в интерфейсе.
- **Text processing models and technical fragments**: Image/video generators (Nano Banana, Imagen, Veo, and equivalents) are excluded; URLs, email addresses, paths, code, tags, versions, and identifiers are protected from model edits. Internal context estimation accounts for Cyrillic without adding UI token counters.
- **Обработка текста — надёжность streaming**: Состояние AI-подключения обновляется после завершения потока, зависший поток ограничен тайм-аутом бездействия, а обновления редактора сгруппированы для плавной работы с длинными ответами.
- **Text processing streaming reliability**: AI connection health now updates after stream completion, stalled streams have an inactivity timeout, and editor refreshes are throttled for smooth long-response rendering.
- **Обработка текста — строка состояния**: Сообщение об использованной модели и таймер перенесены в нижнюю строку редактора, поэтому они больше не изменяют высоту текстового поля и не обрезают счётчики.
- **Text processing status line**: Model usage and progress now appear in the editor footer, so they no longer resize the editor or clip its counters.
- **Обработка текста — просмотр изменений**: Кнопка просмотра теперь отражает состояние и меняет надпись с «Показать изменения» на «Скрыть изменения».
- **Text processing changes view**: The comparison button now reflects its state and changes from “Show changes” to “Hide changes”.
- **Надёжность приложения**: Исправлены финальное сохранение Quick Note, гонка запуска/остановки телеметрии, завершение второго экземпляра, удержание Win32 callback, устаревшее асинхронное превью и восстановление окон встроенных утилит.
- **Application reliability**: Fixed Quick Note final-save handling, the telemetry startup/shutdown race, secondary-instance shutdown, Win32 callback lifetime, stale asynchronous previews, and built-in utility window restoration.
- **Панель и настройки**: Стабилизированы размеры панели между краями и контекстами, сохранение настроек и позиционирование индикатора на нескольких мониторах и при изменении DPI.
- **Panel and settings**: Stabilized panel sizing across edges and contexts, settings persistence, and multi-monitor/DPI-aware indicator positioning.
- **Компактный таймер**: Восстановлено отображение глифов запуска/паузы и разворачивания после унификации стилей кнопок.
- **Compact timer**: Restored the start/pause and expand glyphs after command-button style unification.

## [1.11.1] - 2026-07-15

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Quick Note data safety**: Окно заметки больше не закрывается, если финальное сохранение содержимого завершилось ошибкой.
- **Quick Note data safety**: The note window no longer closes when its final content save fails.
- **Runtime reliability**: Устранена гонка между инициализацией и остановкой телеметрии; ошибка снятия Win32 mouse hook больше не приводит к потере callback delegate.
- **Runtime reliability**: Closed the telemetry initialization/shutdown race and retained the Win32 mouse-hook callback when unhooking fails.
- **Single-instance startup**: Второй экземпляр больше не пытается освободить mutex, которым владеет основной процесс, и не пишет необработанное исключение при завершении.
- **Single-instance startup**: A second instance no longer attempts to release the primary process mutex or logs an unhandled shutdown exception.
- **Icon preview**: Устаревшее асинхронное чтение изображения больше не может перезаписать более новое превью.
- **Icon preview**: A stale asynchronous image read can no longer overwrite a newer preview.
- **Release artifacts**: Локальная сборка инсталлятора теперь обновляет `SHA256SUMS.txt` после опциональной подписи.
- **Release artifacts**: Local installer builds now refresh `SHA256SUMS.txt` after optional signing.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Command buttons**: Основные и вторичные действия в таймере, сортировщике, конвертере ICO, генераторе QR, редакторе кнопки и настройках программы используют единую геометрию, типографику и состояния взаимодействия.
- **Command buttons**: Primary and secondary actions in Timer, File Sorter, ICO Converter, QR Generator, button editor, and application settings now share consistent geometry, typography, and interaction states.
- **Clipboard Manager window**: Кнопка сворачивания скрывает окно обратно в AiteBar без закрытия; повторное нажатие кнопки утилиты восстанавливает тот же экземпляр с сохранённым состоянием.
- **Clipboard Manager window**: Minimize hides the window back into AiteBar without closing it; pressing the utility button again restores the same instance with its state intact.
- **Internal architecture**: Централизованы описания встроенных утилит, снижены аллокации при refresh панели, добавлено кэширование профилей браузеров и улучшена изоляция async/UI-путей.
- **Internal architecture**: Centralized built-in utility metadata, reduced panel-refresh allocations, cached browser profiles, and improved async/UI-path isolation.

## [1.11.0] - 2026-07-13

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Quick Note formatting**: Добавлены заголовки, зачёркивание, ссылки, визуальные маркированные и нумерованные списки, а также сохранение расширенного форматирования через Markdown-compatible разметку.
- **Quick Note formatting**: Added headings, strikethrough, links, visual bullet and numbered lists, and persistence of extended formatting through Markdown-compatible markup.
- **Clipboard Manager workflow**: Добавлены копирование текста в одну строку, расширенные фильтры и операции очистки, закрепление записей и настройка сохранения истории между сессиями.
- **Clipboard Manager workflow**: Added single-line text copying, expanded filters and clear actions, pinned entries, and a setting for history persistence between sessions.
- **QR Code workflow**: Добавлены сочетания `Ctrl+S` для сохранения PNG и `Ctrl+C` для копирования готового QR-кода, когда фокус не находится в текстовом редакторе.
- **QR Code workflow**: Added `Ctrl+S` to save PNG and `Ctrl+C` to copy the generated QR code when focus is outside a text editor.
- **Project policies**: Добавлены MIT license, privacy policy, обновлённая security policy и Markdown-справка по сторонним компонентам.
- **Project policies**: Added the MIT license, privacy policy, updated security policy, and Markdown third-party notices.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Поддержка проекта**: Пункты поддержки в tray-меню и окне «О программе» теперь ведут на `https://codebdbd.github.io/`; ссылка в About переименована в «Поддержать проект».
- **Project support**: Support actions in the tray menu and About window now open `https://codebdbd.github.io/`; the About link is renamed to “Support the project”.
- **Program settings**: Общие настройки получили компактные segmented controls для языка, края панели, размера, зоны активации и задержки; существующие нестандартные значения и выбранные дополнительные мониторы сохраняются до явного изменения пользователем.
- **Program settings**: General settings now use compact segmented controls for language, panel edge, size, activation zone, and delay; existing custom values and selected secondary monitors remain intact until the user explicitly changes them.
- **Panel behavior**: Улучшены перенос кнопок, стабильность размеров между контекстами, клавиатурная навигация, drag-and-drop края/монитора и атомарное сохранение настроек.
- **Panel behavior**: Improved button wrapping, size stability across contexts, keyboard navigation, edge/monitor drag-and-drop, and atomic settings updates.
- **Clipboard Manager reliability**: Длинный текст теперь ограничивается безопасным лимитом вместо полного отбрасывания, история сохраняется атомарно, а список виртуализирован без увеличения лимитов памяти.
- **Clipboard Manager reliability**: Long text is now truncated to the safe limit instead of being discarded, history is saved atomically, and the list is virtualized without increasing memory limits.
- **QR Code reliability**: Нормализация опций и payload объединена в один проход, расширены focused tests генерации, цветов и типов содержимого.
- **QR Code reliability**: Option and payload normalization now use one pass, with expanded focused tests for generation, colors, and content types.
- **Documentation**: Обновлены README, руководство пользователя, карта функций, security/privacy guidance и индекс технической документации.
- **Documentation**: Updated README, user manual, feature map, security/privacy guidance, and the technical documentation index.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Program settings data safety**: Исправлено незаметное округление существующих размеров панели и задержки при сохранении несвязанных настроек, а также сброс третьего и последующих мониторов на монитор №2.
- **Program settings data safety**: Fixed silent rounding of existing panel sizes and delays while saving unrelated settings, and prevented third-or-later monitor selections from being reset to monitor 2.
- **QR Code shortcuts**: `Ctrl+C` больше не перехватывает обычное копирование выделенного текста в полях QR Code Generator.
- **QR Code shortcuts**: `Ctrl+C` no longer overrides normal selected-text copying inside QR Code Generator fields.
- **Quick Note formatting**: Исправлено форматирование маркированных и нумерованных списков: списки теперь отображаются визуально, как в Windows Notepad, без вставки Markdown-маркеров в текст редактора.
- **Quick Note formatting**: Fixed bullet and numbered list formatting: lists are now rendered visually, similar to Windows Notepad, without inserting Markdown markers into the editor text.
- **Quick Note clear formatting**: Очистка форматирования теперь удаляет визуальные списки и не переносит выделение на соседние строки.
- **Quick Note clear formatting**: Clear formatting now removes visual lists and no longer moves selection to unrelated lines.
- **Quick Note clear formatting**: Исправлена очистка выделенного списка из нескольких строк: команда больше не очищает только последний пункт.
- **Quick Note clear formatting**: Fixed clearing multi-line selected lists so the command no longer clears only the last item.
- **Quick Note performance**: Снижены микрозависания при выделении и форматировании текста за счет отложенного обновления статистики, сохранения live-выделения и локальной проверки ссылок.
- **Quick Note performance**: Reduced small stalls during selection and formatting by debouncing footer statistics, preserving live selection, and using local link detection.
- **Quick Note startup**: Текст заметки теперь загружается до первого показа окна, чтобы окно не открывалось пустым с последующим появлением содержимого.
- **Quick Note startup**: Note text is now loaded before the first window paint, avoiding an empty window followed by delayed content.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Quick Note toolbar**: Меню списков теперь содержит только два действия: маркированный список и нумерованный список.
- **Quick Note toolbar**: The list menu now contains only two actions: bullet list and numbered list.
- **Quick Note status**: При выделении текста статус показывает количество символов с пробелами и без пробелов.
- **Quick Note status**: When text is selected, the status area shows character counts with and without spaces.

## [1.10.0] - 2026-06-18

### 🇷🇺 Добавлено | 🇬🇧 Added
- **Индикатор положения панели**: Добавлен индикатор положения панели на панели задач, показывающий направление панели с помощью стрелки и контекстное меню.
- **Panel position indicator**: Added panel position indicator on taskbar showing panel direction with arrow and context menu.
- **Утилита генератора QR-кодов**: Встроенная утилита для создания QR-кодов с настраиваемым размером модуля, уровнем коррекции ошибок и экспортом в PNG/SVG.
- **QR code generator utility**: Built-in utility for generating QR codes with customizable module size, error correction level, and PNG/SVG export.
- **Утилита показа рабочего стола**: Быстрый доступ к показу рабочего стола.
- **Show desktop utility**: Quick access to show desktop.
- **Утилита папки приложений**: Быстрый доступ к папке Applications в Windows.
- **Apps folder utility**: Quick access to Windows Applications folder.
- **Утилита Copilot**: Быстрый запуск Windows Copilot.
- **Copilot utility**: Quick launch of Windows Copilot.
- **Атрибут [Utility]**: Добавлен атрибут для маркировки утилит для автоматической регистрации.
- **Utility attribute**: Added attribute to mark utilities for automatic registration.
- **Метод RegisterAllFromAssembly()**: Автоматическая регистрация всех утилит с атрибутом [Utility] из указанной сборки.
- **RegisterAllFromAssembly() method**: Automatic registration of all [Utility]-marked utilities from an assembly.
- **Версионирование контрактов**: Добавлены `ContractVersion` и `IsCompatibleWith()` в `IUtility` для проверки совместимости.
- **Contract versioning**: Added `ContractVersion` and `IsCompatibleWith()` in `IUtility` for compatibility checks.
- **Изоляция ошибок утилит**: Обернуты `LaunchAsync()` в `UtilityBase` и `ColorPickerUtility` в try/catch с логированием, телеметрией и показом пользователю сообщения вместо падения приложения.
- **Utility error isolation**: Wrapped `LaunchAsync()` in `UtilityBase` and `ColorPickerUtility` with try/catch, logging, telemetry, and user message instead of crashing the app.
- **Тесты UtilityRegistry**: Добавлены 4 теста для проверки регистрации, совместимости, автоматической регистрации и обработки ошибок.
- **UtilityRegistry tests**: Added 4 tests to check registration, compatibility, auto-registration, and error handling.
- **Метод Clear() для тестов**: Добавлен в `UtilityRegistry` для сброса состояния между тестами.
- **Clear() method for testing**: Added to `UtilityRegistry` to reset state between tests.

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Доработка Tooltip**: Улучшено визуальное оформление подсказок, мгновенное появление и правильное центрирование.
- **Tooltip overhaul**: Improved tooltip visual design, instant appearance, and proper centering.
- **Стиль таймера/секундомера**: Обновлён цветовой стиль для соответствия другим утилитам.
- **Timer/stopwatch styling**: Updated color scheme to match other utilities.
- **Прозрачность фона утилит**: Убрана прозрачность фона для снижения когнитивной нагрузки.
- **Utility background transparency**: Removed background transparency to reduce cognitive load.
- **Иконка утилиты конвертации иконок**: Изменена иконка IconConverterUtility на новый глиф.
- **Icon converter utility icon**: Changed IconConverterUtility icon to new glyph.
- **Регистрация утилит**: Заменена ручная регистрация в `App.xaml.cs` на автоматическую через `RegisterAllFromAssembly()`.
- **Utility registration**: Replaced manual registration in `App.xaml.cs` with automatic via `RegisterAllFromAssembly()`.
- **Все утилиты**: Добавлены атрибуты [Utility] к `QuickNoteUtility`, `TimerStopwatchUtility`, `ColorPickerUtility`, `FileSorterUtility`, `IconConverterUtility`.
- **All utilities**: Added [Utility] attributes to `QuickNoteUtility`, `TimerStopwatchUtility`, `ColorPickerUtility`, `FileSorterUtility`, `IconConverterUtility`.
- **Документация**: Обновлена `docs/UTILITIES.md` с описанием новых механизмов.
- **Documentation**: Updated `docs/UTILITIES.md` with new mechanisms.
- **Централизация FindExecutableOnPath**: Метод `FindExecutableOnPath` перенесен в `PathHelper` для избежания дублирования кода между `ActionService` и `SettingsWindow`.
- **Centralize FindExecutableOnPath**: Moved `FindExecutableOnPath` to `PathHelper` to avoid code duplication between `ActionService` and `SettingsWindow`.
- **Индикатор положения панели**: Индикатор теперь автоматически скрывается при запуске приложения в полноэкранном режиме.
- **Panel position indicator**: Indicator now automatically hides when an app is running in fullscreen mode.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Перетаскивание кнопок в многостолбчатом режиме**: Исправлено перетаскивание кнопок между колонками в вертикальном режиме панели. Теперь кнопка может свободно двигаться по обеим осям и корректно отображается в новом положении.
- **Button drag-and-drop in multi-column mode**: Fixed button dragging between columns in vertical panel mode. Buttons can now move freely in both axes and are correctly animated to their new positions.
- **Завершение приложения**: Исправлено зависание процесса при закрытии приложения из-за индикатора положения панели.
- **App shutdown**: Fixed process hanging on app exit due to panel position indicator.
- **Отображение индикатора**: Исправлено не отображение индикатора при включении в настройках и его исчезновение.
- **Indicator visibility**: Fixed indicator not appearing when enabled in settings and its disappearance.
- **Очистка старых conflict copies в Quick Note**: Добавлена автоматическая очистка старых файлов conflict copies (хранятся только последние 5).
- **Cleanup old Quick Note conflict copies**: Added automatic cleanup of old conflict copy files (only last 5 are kept).

## [1.9.1] - 2026-06-15

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Снижение лимита размера настроек**: уменьшено максимальное допустимое значение settings.json с 100MB до 10MB для более быстрой работы и защиты от потенциально опасных больших файлов.
- **Reduced settings file size limit**: changed maximum allowed size of settings.json from 100MB to 10MB for faster performance and protection from potentially dangerous large files.
- **Форматирование кода**: запущен `dotnet format` для исправления mixed whitespace и end-of-line.
- **Code formatting**: ran `dotnet format` to fix mixed whitespace and end-of-line issues.
- **Обновление документации**: исправлены данные в `docs/architecture.md` (MaxUserBands, ActionService, добавлены новые компоненты).
- **Updated documentation**: fixed information in `docs/architecture.md` (MaxUserBands, ActionService, added new components).

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Извлечение иконок при добавлении программ**: исправлено извлечение иконок, проблема в неправильном символе для проверки значения по умолчанию.
- **Icon extraction when adding programs**: fixed icon extraction by checking for correct default icon glyph.
- **Функция открепления/скрытия утилит**: исправлено сохранение настроек, изменения видимости теперь сохраняются и применяются правильно.
- **Unpin/hide utility function**: fixed settings persistence so utility visibility changes are saved and applied correctly.
- **Переключение панелей**: исправлено сохранение активной панели, переключение контекстов теперь правильно сохраняется.
- **Panel switching**: fixed active panel persistence so context switches are saved correctly.
- **Перетаскивание панели**: исправлено сохранение нового края и монитора при перетаскивании панели.
- **Panel dragging**: fixed saving of new edge and monitor when dragging the panel.
- **Настройки приложения**: исправлено сохранение настроек из окна настроек (включая видимость панелей и другие параметры).
- **App settings**: fixed saving changes from settings window (including panel visibility and other options).
- **Генерация дублирующих имен**: добавлен guard на бесконечный цикл в `BuildDuplicateElementName` и fallback на GUID.
- **Duplicate name generation**: added infinite loop guard in `BuildDuplicateElementName` and GUID fallback.

## [1.8.0] - 2026-06-14

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Расчет геометрии панели**: размеры панели теперь проходят через единый путь `CalculateAvailableSize()` / `ComputePanelMetrics()`, чтобы `RefreshPanel()` и смена ориентации использовали одинаковые входные ограничения.
- **Panel layout calculation**: panel sizing now flows through the shared `CalculateAvailableSize()` / `ComputePanelMetrics()` path so `RefreshPanel()` and orientation changes use the same available-size constraints.
- **Позиционирование панели**: чистая математика координат панели и выбора ближайшего края вынесена в `PanelPositionHelper` и покрыта unit-тестами.
- **Panel positioning**: pure panel-coordinate math and nearest-edge selection were extracted to `PanelPositionHelper` and covered with unit tests.
- **Логирование и фоновые операции**: добавлен неблокирующий `Logger.LogAsync()`, deferred startup получил cancellation/fault handling, а проверка обновлений использует общий `HttpClient`.
- **Logging and background work**: added non-blocking `Logger.LogAsync()`, deferred startup now has cancellation/fault handling, and update checks use a shared `HttpClient`.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Безопасность команд**: подтверждение команд теперь дополнительно предупреждает о chaining/redirection и потенциально разрушительных командах вроде `del`, `format`, `shutdown`, `diskpart`.
- **Command safety**: command confirmation now adds an extra warning for chaining/redirection and potentially destructive commands such as `del`, `format`, `shutdown`, and `diskpart`.
- **Надежность сохранения настроек**: `settings.json` записывается через временный файл и замену с бэкапом предыдущей версии, снижая риск повреждения настроек при ошибке записи.
- **Settings persistence reliability**: `settings.json` is written through a temporary file and replacement with a backup of the previous version, reducing the risk of corrupt settings after write failures.
- **Завершение приложения**: Sentry flush больше не блокирует поток выхода синхронным ожиданием, а cleanup панели устойчивее к ошибкам освобождения hook/tray ресурсов.
- **Application shutdown**: Sentry flush no longer blocks exit with synchronous waiting, and panel cleanup is more resilient to hook/tray disposal failures.
- **Профили браузеров**: Chromium `Preferences` читаются с `FileShare.ReadWrite`, чтобы открытый браузер реже мешал отображению профилей.
- **Browser profiles**: Chromium `Preferences` are read with `FileShare.ReadWrite`, so a running browser is less likely to block profile display.
- **Вертикальный режим панели**: кнопка настроек теперь отображается корректно в вертикальном положении, устранено лишнее пустое пространство.
- **Vertical panel mode**: settings button now appears correctly in vertical orientation, removed extra empty space.

### 🇷🇺 Тесты | 🇬🇧 Tests
- **Регрессии надежности**: добавлены проверки предупреждений для опасных команд, безопасного backup-сохранения настроек, async logging и чтения browser preferences при параллельном доступе.
- **Reliability regressions**: added checks for dangerous-command warnings, safe backup-based settings persistence, async logging, and browser preference reads under concurrent access.
- **Вертикальный режим панели**: обновлены тесты `PanelLayoutHelperTests` для нового поведения.
- **Vertical panel mode**: updated `PanelLayoutHelperTests` for new behavior.

## [1.7.9] - 2026-06-13

### 🇷🇺 Изменено | 🇬🇧 Changed
- **Единый runtime-механизм локализации**: `LocalizationService` теперь хранит активную культуру приложения и выдает строки независимо от культурного состояния вызывающего потока.
- **Unified runtime localization mechanism**: `LocalizationService` now keeps the active app culture and resolves strings independently from the caller thread culture.
- **Обновление локализуемых окон и списков**: окна настроек, утилиты и динамические списки перестраивают локализованные элементы через общий механизм `CultureChanged`.
- **Localized window and list refresh**: settings windows, utility windows and dynamic lists now rebuild localized elements through the shared `CultureChanged` mechanism.

### 🇷🇺 Исправлено | 🇬🇧 Fixed
- **Tray-меню и контекстные меню**: перевод теперь применяется сразу после смены языка без перезапуска, включая меню в трее, контекстные меню кнопок, панелей и встроенных инструментов.
- **Tray menu and context menus**: translations now switch immediately after a language change without restart, including tray, button, panel and built-in tool context menus.
- **Новые окна после смены языка**: диалоги и окна, открытые после переключения языка, теперь создаются сразу в выбранной культуре.
- **New windows after language switch**: dialogs and windows opened after changing language now start directly in the selected culture.
- **Имена панелей по умолчанию и экспорт**: отображаемые имена панелей локализуются в UI и export/import сценариях без записи переведенного текста в настройки.
- **Default panel names and export**: panel display names are localized in UI and export/import flows without storing translated text in settings.

### 🇷🇺 Тесты | 🇬🇧 Tests
- **Регрессия локализации**: добавлены проверки межпоточной локализации, обновления detached `ContextMenu`, целостности `.resx`-ключей и поведения `ApplyCulture`.
- **Localization regression coverage**: added checks for cross-thread localization, detached `ContextMenu` refresh, `.resx` key integrity and `ApplyCulture` behavior.

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
