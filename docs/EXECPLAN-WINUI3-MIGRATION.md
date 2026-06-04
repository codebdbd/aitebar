# Инструкции для AI-агента: миграция AiteBar с WPF на WinUI 3

## Главная цель

Мигрировать приложение AiteBar с WPF на WinUI 3 / Windows App SDK так, чтобы приложение выглядело и вело себя как нативное Windows 11-приложение с полноценной поддержкой Fluent Design System.

Результат должен быть не косметическим переносом XAML, а полноценной архитектурной миграцией UI, ресурсов, оконной модели, навигации, тем, иконок, анимаций и поведения под Windows 11.

## Ключевые решения (фиксированные)

1. **Стратегия миграции**: Параллельные проекты (WPF-версия остаётся как есть, создаётся новый WinUI 3-проект)
2. **MVVM-фреймворк**: CommunityToolkit.Mvvm
3. **Windows App SDK версия**: 1.6 LTS (стабильная версия на июнь 2026)
4. **Тип деплоя**: Unpackaged (простота для desktop-приложения с tray)
5. **NotifyIcon**: Оставить WinForms NotifyIcon на первом этапе (нет штатной замены в WinUI 3), изолировать через `INotifyIconService` интерфейс
6. **Поддерживаемые ОС**: Windows 10 1903+ и Windows 11
   - Windows 11: Использовать Mica для фона окон
   - Windows 10: Fallback на Acrylic или SolidColorBrush (проверять через `Environment.OSVersion` или `DesktopWindowManager.IsCompositionEnabled`)
7. **OverflowWrapPanel**: Адаптировать существующий контрол под WinUI 3 (сохранить логику MaxUserBands = 2)
8. **Dependency Injection**: Использовать `Microsoft.Extensions.DependencyInjection` для управления сервисами и ViewModel
9. **Один экземпляр приложения**: Использовать `AppInstance.FindOrRegisterForKey` из Windows App SDK
10. **Обработка WM_HOTKEY**: Субклассирование окна через Win32 interop + `SetWindowSubclass`
11. **Mouse hook**: NativeIntegrationService в отдельном STA-потоке, диспатч в DispatcherQueue
12. **Локализация**: Адаптировать существующий `LocalizationService` и `LocExtension` под WinUI 3 (не использовать .resw)
13. **Иконки**: Поддержать три источника: Segoe Fluent Icons (нативно), Material Symbols и Font Awesome Brands (как шрифтовые ресурсы)

## Контракт (неизменяемое при миграции)

Эти вещи **не должны меняться** - пользователи полагаются на них:

### 1. Формат и расположение файлов
- Путь к данным: `%AppData%\Codebdbd\Aite Bar`
- Формат и содержимое:
  - `settings.json` (основные настройки)
  - `config.json` (старый формат для миграции)
  - `QuickNote.md` (быстрая заметка)
  - `Icons` папка с пользовательскими иконками
  - `error.log` и `error.log.bak` (логи ошибок)
  - Бэкапы `settings.json.backup.0-4`
- Формат бэкапов настроек (ротация 5 последних версий)
- Формат `.aitebarpanel` для импорта/экспорта панелей

### 2. Основные сценарии пользователя
#### Панель
- **Показ панели**:
  - Наведение курсора на край экрана (с задержкой `ActivationDelayMs`)
  - Клик по tray-значку
  - Глобальная горячая клавиша
- **Скрытие панели**:
  - Клик вне панели
  - Запуск большинства действий
  - Глобальная горячая клавиша (повторно)
  - Эскейп
- **Перемещение панели**:
  - Drag-and-drop за handle на любой край экрана (Top/Bottom/Left/Right)
  - Перетаскивание на другой монитор
  - Сохранение выбранного края и монитора в настройках
- **Переключение контекстов**:
  - Колесо мыши на панели (с задержкой `ContextWheelSwitchCooldown`)
  - Горячие клавиши Next/Previous Context
  - Контекстное меню панели
- **Порядок кнопок**:
  - Drag-and-drop пользовательских кнопок для изменения порядка
- **Анимации**:
  - Появление (175 мс)
  - Скрытие (140 мс)
  - Переключение контекстов
- **Первичная панель (Primary Context)**:
  - Только на первичной панели показываются системные утилиты (Search, Screenshot, Calc, etc.)

#### Управление кнопками
- **Добавление кнопки**:
  - Кнопка `+` на панели
  - Горячая клавиша AddButtonHotkey
  - Все типы действий (Web, Hotkey, Program, File, Folder, ScriptFile, Command)
- **Редактирование кнопки**:
  - ПКМ → Редактировать
  - Изменение всех параметров
- **Дублирование кнопки**:
  - ПКМ → Дублировать
  - После дублирования автоматически открывается окно настроек для новой кнопки
- **Переименование кнопки**:
  - ПКМ → Переименовать
  - Используется `TextPromptDialog` для ввода нового имени
- **Перемещение кнопки**:
  - ПКМ → Переместить (в другую панель)
- **Копирование информации**:
  - ПКМ → Копировать URL (для Web)
  - ПКМ → Копировать путь (для File/Folder/Program)
  - ПКМ → Копировать команду (для Command/Script)
- **Открытие расположения**:
  - ПКМ → Открыть расположение (для File/Folder/Program)
- **Удаление кнопки**:
  - ПКМ → Удалить (с подтверждением)
- **Отключение/включение инструментов**:
  - ПКМ по инструменту → Открепить
  - В настройках можно включить обратно

### 3. Поддерживаемые функции
#### Типы действий
- **Web**:
  - Все браузеры (Chrome, Edge, Brave, Yandex, Firefox, Opera, OperaGX, Vivaldi)
  - Профили браузеров
  - Ротация профилей
  - App Mode (для Chromium)
  - Incognito/Private режим
  - Fullscreen
  - Открытие в выбранном браузере
- **Program**:
  - Запуск `.exe`, `.lnk`, `.appref-ms`
  - Topmost режим
- **File**:
  - Открытие через системную ассоциацию
- **Folder**:
  - Открытие в проводнике
- **ScriptFile**:
  - Запуск `.bat`, `.cmd`, `.ps1`, `.py`
  - Подтверждение перед запуском
  - Проверка наличия Python для `.py`
- **Command**:
  - Запуск командной строки
  - Подтверждение перед запуском
- **Hotkey**:
  - Отправка сочетания клавиш в активное окно

#### Встроенные инструменты
- **Поиск**: Поиск текста из буфера обмена в Google
- **Скриншот**: Открытие `ms-screenclip:`
- **Запись видео**: Открытие `ms-screenclip:?type=recording`
- **Калькулятор**: Запуск `calc.exe`
- **Проводник**: Запуск `explorer.exe`
- **Загрузки**: Открытие `shell:Downloads`
- **File Sorter**: Сортировка файлов в папке по типам, с undo
- **Таймер/Секундомер**:
  - Таймер с пресетами (1-120 мин)
  - Свой ввод времени
  - Старт/Пауза/Сброс
  - Секундомер с сотыми долями секунды
  - Компактный режим
  - Звук окончания (опционально)
  - Сохранение последнего режима и настроек
- **Выбор цвета**: Overlay-пипетка, копирование HEX в буфер обмена
- **Quick Note**:
  - Автосохранение в `QuickNote.md`
  - Базовое форматирование
  - Открытие во внешнем редакторе
  - Очистка с подтверждением
  - Темы (тёмная/светлая)
  - Изменение размера и положения окна
  - Закрытие по Esc и по клику вне окна
  - `Ctrl+Shift+C` - копировать всё
  - `Ctrl+клик` - открыть URL

#### Окна приложения
- **SettingsWindow**: Окно добавления/редактирования кнопки
  - Выбор типа действия
  - Выбор иконки (через `IconPickerWindow`)
  - Выбор цвета
  - Настройки браузера, профиля, ротации (через `RotationProfileSelectionWindow`)
  - Горячие клавиши
- **AppSettingsWindow**: Окно настроек приложения
  - Язык
  - Настройки панели
  - Горячие клавиши
  - Управление контекстами
- **IconPickerWindow**: Окно выбора иконки
  - Fluent System Icons
  - Пользовательские изображения
- **RotationProfileSelectionWindow**: Окно выбора профилей для ротации
- **TextPromptDialog**: Диалог для ввода текста (переименование)
- **AboutWindow**: Окно "О программе"
  - Проверка обновлений
  - Поддержать автора
- **FileSorterWindow**: Окно File Sorter
- **TimerStopwatchWindow**: Окно таймера/секундомера
- **QuickNoteWindow**: Окно Quick Note
- **ScreenColorPickerWindow**: Окно выбора цвета

#### Панели-контексты
- 8 панелей по умолчанию
- Добавление/удаление/переименование панелей
- Включение/отключение панелей
- Выбор иконки и цвета для панели
- Импорт панелей из `.aitebarpanel`
- Экспорт панелей в `.aitebarpanel` (включая иконки)

#### Горячие клавиши
- Глобальная горячая клавиша для показа/скрытия панели
- Глобальная горячая клавиша для следующей панели
- Глобальная горячая клавиша для предыдущей панели
- Глобальная горячая клавиша для добавления кнопки
- Глобальная горячая клавиша для File Sorter
- Глобальная горячая клавиша для Quick Note
- Глобальная горячая клавиша для Color Picker
- Глобальная горячая клавиша для Timer Stopwatch
- Отдельная глобальная горячая клавиша для каждой пользовательской кнопки
- Поддерживаемые модификаторы: Ctrl, Alt, Shift, Win
- Поддерживаемые клавиши: Space, [], A-Z, 0-9, NumPad, F1-F12

#### Настройки
- Язык интерфейса (auto + все поддерживаемые языки)
- Сторона панели (Top/Bottom/Left/Right)
- Монитор
- Размер зоны активации (%)
- Размер панели (%)
- Задержка появления (мс)
- Включение/отключение каждого инструмента
- Звук таймера
- Тема Quick Note
- Автозапуск
- Светлая/тёмная тема интерфейса

#### Tray-меню
- Открыть
- Настройки программы
- О программе
- Поддержать автора
- Закрыть и выйти

#### Системная интеграция
- Один экземпляр приложения (проверка через Mutex; при повторном запуске показывается сообщение, что приложение уже работает)
- Автозапуск через `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Tray-значок
- Глобальные горячие клавиши
- Win32 interop для позиционирования окон, отправки клавиатуры, mouse hook

#### Прочие функции
- Проверка обновлений (через `UpdateCheckService`)
- Логирование ошибок в `error.log` с ротацией (сохраняется backup)
- Телеметрия (через `TelemetryService`; не изменяется)
- Обработка необработанных исключений (с логированием)

#### Локализация
- Все локализованные строки и ресурсы
- Поддерживаемые языки: ru, en, uk, de (и другие, если есть)

### 4. Поведение интерфейса
- **Клавиатурная навигация**:
  - Tab/Shift+Tab для переключения между кнопками
  - Enter/Пробел для запуска
  - Стрелки для навигации
  - Esc для закрытия
- **Фокус**:
  - Визуальная индикация фокуса
  - Фокус при открытии панели
- **Accessibility**:
  - AutomationProperties.Name для всех интерактивных элементов
  - Поддержка скринридеров
- **DPI scaling**:
  - Поддержка всех масштабов (100%, 125%, 150%, 200%)
- **High Contrast**:
  - Поддержка системной темы с высоким контрастом

### 5. Данные пользователя
- **Все существующие CustomElement'ы должны работать**:
  - Сохраняются ID кнопок и контекстов
  - Сохраняются цвета, иконки, параметры действий
  - Сохраняются HotkeyBinding'ы
  - Сохраняются профили браузеров
  - Сохраняются настройки ротации профилей
- **Совместимость**:
  - WinUI 3-версия должна корректно загружать настройки из WPF-версии
  - WPF-версия должна корректно загружать настройки из WinUI 3-версии (обратная совместимость)
- **Бэкапы**:
  - Автоматическое создание бэкапов перед сохранением
  - Ротация 5 последних бэкапов

## Основные правила

1. Не переносить WPF-код механически один в один.
2. Не имитировать Windows 11 вручную через кастомные стили, если есть штатные WinUI 3-контролы.
3. Не использовать WPF-зависимости в новом UI-слое.
4. Не смешивать `System.Windows.*` и `Microsoft.UI.Xaml.*`.
5. Не использовать старые WPF-паттерны там, где в WinUI 3 есть штатная замена.
6. Каждый экран должен выглядеть как современное Windows 11-приложение: Fluent, Mica/Acrylic где уместно, корректные отступы, радиусы, типографика, hover/focus/pressed-состояния.
7. Код должен быть поддерживаемым, модульным и пригодным для дальнейшего развития.

## Целевое состояние проекта

После миграции проект должен использовать:

- WinUI 3 с Windows App SDK 1.6 LTS;
- `Microsoft.UI.Xaml`;
- `Microsoft.UI.Windowing`;
- `Microsoft.UI.Dispatching`;
- `Microsoft.UI.Composition` для визуальных эффектов;
- CommunityToolkit.Mvvm для MVVM;
- `ThemeResource` вместо WPF `DynamicResource`;
- WinUI NavigationView / TabView / CommandBar / TeachingTip / InfoBar / ContentDialog там, где это подходит;
- Segoe UI Variable для текста;
- Segoe Fluent Icons для иконок;
- нативные WinUI-стили без самодельного "псевдо-Fluent".

## Строгие запреты

Запрещено:

- оставлять WPF UI-контролы в новом WinUI UI-слое;
- использовать `System.Windows.Controls`;
- использовать `System.Windows.Media`;
- использовать `System.Windows.Threading.Dispatcher`;
- использовать `WindowStyle=None` и вручную рисовать весь chrome без необходимости;
- копировать WPF XAML без адаптации;
- использовать старые Material Design / MahApps / ModernWpf-стили вместо WinUI 3;
- вручную задавать цвета вместо `ThemeResource`, если цвет относится к теме;
- хардкодить Light/Dark theme;
- ломать accessibility;
- удалять функциональность ради "быстрого переноса";
- менять бизнес-логику без необходимости;
- игнорировать DPI, масштабирование, клавиатурную навигацию и screen reader.

## Обязательные замены WPF → WinUI 3

Использовать такие правила миграции:

- `System.Windows.*` → `Microsoft.UI.Xaml.*`
- `Window` WPF → `Microsoft.UI.Xaml.Window`
- `Application` WPF → `Microsoft.UI.Xaml.Application`
- `Dispatcher.Invoke` / `BeginInvoke` → `DispatcherQueue.TryEnqueue`
- `INotifyPropertyChanged` → использовать `ObservableObject` из CommunityToolkit.Mvvm
- `{Binding}` → `{x:Bind}`:
  - Использовать {x:Bind} в большинстве случаев, где DataContext явно задан и типизирован
  - {x:Bind} даёт compile-time проверку, лучше для производительности
  - Продолжать использовать {Binding} только там, где {x:Bind} не подходит (например, ItemTemplate в списках без явной типизации)
- `DynamicResource` → `ThemeResource`:
  - В WinUI 3 нет DynamicResource, используйте ThemeResource для темозависимых ресурсов
  - Для статических ресурсов, не зависящих от темы, продолжайте использовать StaticResource
- `StaticResource` оставить, если ресурс не зависит от темы
- `RoutedCommand` / WPF Commands → `RelayCommand` / `AsyncRelayCommand` из CommunityToolkit.Mvvm
- `Window.WindowState` → `AppWindow` + `OverlappedPresenter`
- `PresentationSource` / HWND-доступ → `WinRT.Interop.WindowNative.GetWindowHandle`
- `System.Windows.Media.Brush` → `Microsoft.UI.Xaml.Media.Brush`
- `BitmapImage` namespace заменить на WinUI-compatible namespace
- `ResourceDictionary.MergedDictionaries` адаптировать под WinUI 3
- WPF triggers заменить на `VisualStateManager`, `StateTrigger`, `AdaptiveTrigger`, styles или code-behind там, где оправдано
- `MultiBinding` → не поддерживается в WinUI 3:
  - Заменить на вычисляемые свойства в ViewModel
  - Или использовать IValueConverter с ConverterParameter
- `GridSplitter` → используйте CommunityToolkit.WinUI.Controls.GridSplitter
- `Adorner` → нет прямого аналога, используйте альтернативные подходы (например, Popup или отдельный слой)
- `DrawingBrush` / `VisualBrush` → используйте WinUI Brushes или Composition API
- `Freezable` → не используется в WinUI 3
- `WindowChrome` → ExtendsContentIntoTitleBar
- `SystemParameters` → DisplayInformation или другие WinRT API
- `MouseBinding` → KeyboardAccelerators или события Pointer
- `KeyboardNavigation` → TabFocusNavigation в WinUI 3

## Архитектурная стратегия

Миграцию выполнять **итеративно, по этапам**, используя **параллельные проекты**.

### Этапы миграции

1. **Этап 1: Подготовка инфраструктуры
   - Создать новый WinUI 3-проект `AiteBar.WinUI3`
   - Подключить необходимые NuGet-пакеты (CommunityToolkit.Mvvm, Windows App SDK 1.6, Microsoft.Extensions.DependencyInjection)
   - **Создать отдельную библиотеку AiteBar.Core** (не Shared Project!) для бизнес-логики: сервисы, модели, helpers)
   - Перенести бизнес-логику из WPF-проекта в AiteBar.Core, убирая WPF-зависимости
   - Добавить интерфейсы для фабрик окон (например, `IQuickNoteWindowFactory`, `IScreenColorPickerWindowFactory` и т.д.) для инверсии зависимостей в ActionService
   - Настроить общую сборку и тесты
   - Адаптировать LocalizationService и LocExtension под WinUI 3
   - Настроить Dependency Injection в App.xaml.cs

2. **Этап 2: Миграция простых окон
   - AboutWindow
   - TextPromptDialog
   - Простые вспомогательные окна

3. **Этап 3: Миграция SettingsWindow и AppSettingsWindow
   - SettingsWindow (окно добавления/редактирования кнопки)
     - Вкладки, формы, настройки
   - AppSettingsWindow (окно настроек приложения)
     - Зависимости: AppSettingsService, ContextStateHelper, HotkeyService, LocalizationService
   - Оба окна должны использовать MVVM с CommunityToolkit.Mvvm

4. **Этап 4: Миграция вспомогательных утилит
   - IconPickerWindow
   - RotationProfileSelectionWindow
   - FileSorterWindow
   - TimerStopwatchWindow
   - QuickNoteWindow
   - **ScreenColorPickerWindow**: Использовать Win32 interop для создания прозрачного overlay-окна (WS_EX_LAYERED, SetLayeredWindowAttributes)

5. **Этап 5: Миграция MainWindow (декомпозиция)
   - Разбить MainWindow на отдельные компоненты:
     - PanelVisibilityManager (показ/скрытие панели, mouse hook)
     - TrayIconManager (NotifyIcon, контекстное меню)
     - ButtonPanelManager (RefreshPanel, drag-and-drop кнопок)
     - ContextManager (переключение контекстов)
   - Адаптировать OverflowWrapPanel под WinUI 3 (MeasureOverride/ArrangeOverride)
   - Перенести анимации на Composition API
   - Настроить AppWindow и ExtendsContentIntoTitleBar
   - Поддержать три сценарии drag-and-drop:
     1. Перетаскивание кнопок для изменения порядка (Pointer events)
     2. Перетаскивание панели за handle на другой край/монитор (Win32 interop)
     3. Drag-and-drop файлов/ссылок для создания кнопок (DragEventArgs)
   - Подключить HotkeyService с обработкой WM_HOTKEY через SetWindowSubclass
   - Подключить NativeIntegrationService с mouse hook в отдельном STA-потоке

6. **Этап 6: Интеграция NotifyIcon
   - Реализовать интерфейс `INotifyIconService` с использованием WinForms NotifyIcon (изолировать WPF/WinForms-зависимости)
   - Зарегистрировать сервис в DI
   - Проверить взаимодействие с WinUI 3-окнами
   - В финальном аудите разрешить System.Windows.Forms только в этом сервисе

7. **Этап 7: Финальное тестирование и полировка
   - Все сценарии пользователя
   - Light/Dark тема + Mica fallback для Windows 11 и fallback для Windows 10
   - Accessibility
   - DPI scaling (100%, 125%, 150%, 200%)
   - Performance
   - Async favicon загрузка с обновлением UI через DispatcherQueue
   - Проверка отсутствия утечек памяти

### Слой 1: бизнес-логика

Сохранить существующую бизнес-логику, сервисы, модели, API-клиенты и обработку данных. Не переписывать без причины.

**Рефакторинг ActionService**:
- Убрать прямые зависимости от WPF-окон (QuickNoteWindow, ScreenColorPickerWindow, FileSorterWindow, TimerStopwatchWindow)
- Ввести интерфейсы фабрик окон: `IQuickNoteWindowFactory`, `IScreenColorPickerWindowFactory`, `IFileSorterWindowFactory`, `ITimerStopwatchWindowFactory`
- Внедрить эти фабрики в ActionService через конструктор
- Все UI-обновления из сервисов (например, async favicon загрузка) должны выполняться через IProgress<T> или события, с диспатчем в DispatcherQueue.TryEnqueue

### Слой 2: ViewModel

Использовать CommunityToolkit.Mvvm:
- `ObservableObject` для базовых ViewModel
- `RelayCommand` / `AsyncRelayCommand` для команд
- `ObservableRecipient` для messaging (если нужно)
- Убрать прямые зависимости от WPF/WinUI из ViewModel

### Слой 3: UI

Переписать XAML под WinUI 3. Не копировать WPF-разметку вслепую.

### Слой 4: Windowing

Перенести работу с окном через Windows App SDK:
- `AppWindow`
- `OverlappedPresenter`
- `ExtendsContentIntoTitleBar` для кастомного хрома
- Для MainWindow использовать полностью кастомный хром (как в WPF)
- Tray через WinForms NotifyIcon
- Win32 interop только там, где нужно

### Слой 5: Design System

Создать единый набор ресурсов: typography, spacing, corner radius, brushes, icons, animations, component styles.

## Правила дизайна Windows 11

Приложение должно выглядеть нативно для Windows 11.

Использовать:

- Segoe UI Variable;
- стандартные размеры WinUI-контролов;
- скругления Windows 11;
- мягкие hover/pressed/focus-состояния;
- Mica для обычных окон, Acrylic для transient surfaces;
- системные ThemeResource;
- корректную поддержку Light/Dark/High Contrast;
- NavigationView для основной навигации, если структура приложения это требует;
- CommandBar или AppBarButton для действий;
- TeachingTip / InfoBar для подсказок и уведомлений;
- ContentDialog для подтверждений;
- Flyout/MenuFlyout для контекстных действий;
- NumberBox, ToggleSwitch, ComboBox, AutoSuggestBox, ProgressRing, ProgressBar и другие WinUI-контролы вместо кастомных аналогов.

Не использовать визуальный стиль WPF-era: плоские серые панели, резкие границы, маленькие кнопки, неактуальные иконки, перегруженные формы.

## Типографика

Базовая система:

- основной шрифт: Segoe UI Variable;
- body text: 14 px;
- secondary text: 12 px;
- section title: 16–20 px;
- page title: 24–32 px;
- line-height не зажимать;
- не использовать чрезмерно жирный текст;
- не использовать случайные размеры шрифта.

Текст должен иметь нормальную визуальную иерархию: title, subtitle, body, metadata, helper text.

## Отступы и размеры

Использовать кратные 4 px значения.

Базовые значения:

- 4 px — микроотступ;
- 8 px — малый отступ;
- 12 px — внутренний отступ компактных элементов;
- 16 px — стандартный отступ между блоками;
- 24 px — крупный отступ между секциями;
- 32 px — page margin / крупная структура.

Минимальная высота интерактивных элементов — не делать меньше нативных WinUI-контролов. Не создавать "сжатый WPF-интерфейс".

## Иконки

Использовать:
- Segoe Fluent Icons (нативно через SymbolIcon/FontIcon);
- Material Symbols (подключить как шрифтовой ресурс);
- Font Awesome Brands (подключить как шрифтовой ресурс).

Запрещено:

- использовать старые bitmap-иконки;
- смешивать разные icon pack без причины;
- использовать иконки разной толщины;
- использовать иконку без текстовой подписи там, где смысл неочевиден.

## Темы и ресурсы

Все цвета, зависящие от темы, должны быть через `ThemeResource`.

Пример подхода:

```xml
Foreground="{ThemeResource TextFillColorPrimaryBrush}"
Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"
```

Не хардкодить:

```xml
Foreground="#FFFFFF"
Background="#202020"
```

Исключение — брендовые цвета, если они действительно нужны. Даже брендовые цвета должны иметь Light/Dark-варианты.

## Оконная модель

Для управления окном использовать WinUI 3 + Windows App SDK:

- `AppWindow`;
- `OverlappedPresenter`;
- `ExtendsContentIntoTitleBar` для обычных окон;
- для MainWindow — полностью кастомный хром без системных кнопок;
- корректная drag region;
- поддержка Snap Layouts для обычных окон;
- корректная работа resize/minimize/maximize/restore для обычных окон.

Не ломать стандартное Windows-поведение окна (кроме MainWindow).

## Навигация

Если AiteBar имеет несколько разделов, использовать `NavigationView`.

Правила:

- слева — основная навигация;
- сверху — command area, если нужны частые действия;
- внутри страниц — карточки, списки, формы;
- не делать вложенную навигацию без необходимости;
- текущее состояние навигации должно быть понятно;
- back navigation должна работать ожидаемо.

## Диалоги и уведомления

Использовать:

- `ContentDialog` для подтверждений;
- `InfoBar` для статусных сообщений;
- `TeachingTip` для объяснения новых функций;
- `Flyout` для лёгких transient-действий;
- `MenuFlyout` для контекстных меню.

Не использовать самодельные popup-окна, если есть штатный WinUI-компонент.

## MVVM с CommunityToolkit.Mvvm

Требования:

- Все ViewModel наследуются от `ObservableObject`
- Команды через `RelayCommand` / `AsyncRelayCommand`
- Свойства через `[ObservableProperty]` атрибут (source generators)
- ViewModel не должна зависеть от конкретных UI-контролов
- async-команды не должны блокировать UI
- ошибки должны обрабатываться явно
- long-running операции показывают progress
- UI updates только через `DispatcherQueue.TryEnqueue`, если нужен UI thread

Пример ViewModel:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "AiteBar";

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        // Логика открытия настроек
    }
}
```

## Примеры ключевых компонентов

### Интерфейс `INotifyIconService`
```csharp
// AiteBar.Core/Services/INotifyIconService.cs
using System;

namespace AiteBar.Core.Services;

public interface INotifyIconService : IDisposable
{
    bool IsVisible { get; set; }
    string ToolTipText { get; set; }
    event EventHandler Click;
    event EventHandler DoubleClick;
    
    void ShowContextMenu();
}
```

### Пример фабрики окон
```csharp
// AiteBar.WinUI3/Factories/IQuickNoteWindowFactory.cs
using AiteBar.WinUI3.Windows;

namespace AiteBar.WinUI3.Factories;

public interface IQuickNoteWindowFactory
{
    QuickNoteWindow Create();
}

// AiteBar.WinUI3/Factories/QuickNoteWindowFactory.cs
using Microsoft.Extensions.DependencyInjection;

namespace AiteBar.WinUI3.Factories;

public class QuickNoteWindowFactory(IServiceProvider serviceProvider) : IQuickNoteWindowFactory
{
    public QuickNoteWindow Create() => serviceProvider.GetRequiredService<QuickNoteWindow>();
}
```

### Рефакторинг ActionService с фабриками
```csharp
// AiteBar.Core/Services/ActionService.cs (отрефакторенный)
using AiteBar.Core.Factories;

namespace AiteBar.Core.Services;

public class ActionService(
    IAppSettingsService appSettingsService,
    IBrowserHelper browserHelper,
    IProfileRotationHelper profileRotationHelper,
    IQuickNoteWindowFactory quickNoteFactory,
    IScreenColorPickerWindowFactory colorPickerFactory,
    IFileSorterWindowFactory fileSorterFactory,
    ITimerStopwatchWindowFactory timerFactory)
{
    public void StartQuickNote()
    {
        var window = quickNoteFactory.Create();
        window.Activate();
    }
    
    // Остальные методы...
}
```

### Адаптация LocExtension для WinUI 3
```csharp
// AiteBar.WinUI3/MarkupExtensions/LocExtension.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using AiteBar.Core.Services;

namespace AiteBar.WinUI3.MarkupExtensions;

[MarkupExtensionReturnType(ReturnType = typeof(string))]
public class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;
    
    protected override object ProvideValue()
    {
        return LocalizationService.Get(Key);
    }
}
```

## Async и потоки

Запрещено блокировать UI thread.

Запрещено:

```csharp
Task.Wait();
Task.Result;
Thread.Sleep();
```

Использовать:

```csharp
await;
CancellationToken;
DispatcherQueue.TryEnqueue;
```

Все операции загрузки, сети, файлов и API должны быть async.

## Accessibility

Обязательно проверить:

- keyboard navigation;
- tab order;
- focus states;
- narrator labels;
- automation properties;
- contrast;
- high contrast mode;
- scaling 100%, 125%, 150%, 200%;
- touch target size;
- screen reader compatibility.

Каждый интерактивный элемент должен иметь понятное имя.

Пример:

```xml
AutomationProperties.Name="Open settings"
```

## Производительность

Требования:

- не создавать тяжёлые visual tree без необходимости;
- использовать virtualization для списков;
- избегать больших synchronous операций в UI;
- не пересоздавать UI без причины;
- использовать compiled bindings `{x:Bind}` там, где безопасно;
- не грузить изображения/ресурсы синхронно;
- проверять startup time;
- проверять memory leaks после закрытия окон/страниц.

## Стратегия миграции файлов

Для каждого WPF-файла:

1. Определить назначение файла.
2. Отнести его к категории: View, ViewModel, Model, Service, Resource, Style, Helper.
3. View переписать под WinUI 3.
4. ViewModel очистить от WPF-зависимостей, добавить CommunityToolkit.Mvvm.
5. ResourceDictionary адаптировать.
6. Стили заменить на WinUI-native.
7. Проверить runtime-поведение.
8. Добавить тест/ручную проверку.

## Правила работы с XAML

Перед изменением XAML агент должен:

1. Понять структуру экрана.
2. Определить нативные WinUI-контролы.
3. Удалить WPF-specific свойства.
4. Переписать layout с учётом Windows 11 spacing.
5. Подключить ThemeResource.
6. Проверить Light/Dark.
7. Проверить состояния hover/pressed/disabled/focused.
8. Проверить адаптивность окна.

## Типичные WPF-конструкции, которые нельзя переносить напрямую

Проверить и заменить:

- `GridSplitter`;
- `DataTrigger` → VisualStateManager или адаптивные триггеры;
- `Style.Triggers` → VisualStateManager;
- `MultiBinding` → `MultiBinding` в WinUI 3 (проверить совместимость);
- `RelativeSource` → проверить совместимость;
- `ElementName` binding — проверить совместимость;
- `CommandBinding` → RelayCommand;
- `InputBinding` → KeyboardAccelerators в WinUI 3;
- `Adorner`;
- `DrawingBrush`;
- `VisualBrush`;
- `Freezable`;
- `DependencyProperty` с WPF namespace → DependencyProperty в WinUI 3;
- `UserControl.Resources` с WPF-only ресурсами;
- кастомные `ControlTemplate`, завязанные на WPF → переписать под WinUI 3;
- `WindowChrome` → ExtendsContentIntoTitleBar;
- `SystemParameters` → DisplayInformation или другие WinRT API;
- `MouseBinding` → KeyboardAccelerators или события Pointer;
- `KeyboardNavigation` → TabFocusNavigation в WinUI 3.

Если прямого аналога нет — предложить WinUI-эквивалент, не делать случайный костыль.

## Проверка после каждого изменения

После каждого логического блока миграции агент обязан проверить:

- проект собирается;
- нет WPF namespace в новом UI;
- нет broken bindings;
- нет runtime XAML exceptions;
- Light/Dark работают;
- окно открывается корректно;
- основные сценарии пользователя работают;
- визуально экран соответствует Windows 11 Fluent;
- нет регрессии бизнес-логики;
- unit-тесты бизнес-логики проходят.

## Definition of Done

Миграция считается завершённой только если:

1. AiteBar запускается как WinUI 3-приложение.
2. Основной UI не использует WPF-контролы.
3. Все ключевые сценарии работают.
4. Приложение визуально соответствует Windows 11 Fluent Design.
5. Поддерживаются Light и Dark theme.
6. Нет хардкода системных цветов.
7. Окна ведут себя нативно.
8. Работают resize, minimize, maximize, restore (кроме MainWindow).
9. Нет блокировки UI thread.
10. Нет критических XAML runtime errors.
11. Нет очевидных memory leaks.
12. Работает keyboard navigation.
13. Работают focus states.
14. Работают screen reader labels.
15. Проект можно собрать чисто без ручных исправлений.
16. Удалены или изолированы WPF-зависимости.
17. Документированы спорные места и компромиссы.
18. Все unit-тесты бизнес-логики проходят.

## Формат работы агента

Агент должен работать итерациями.

Для каждой итерации агент обязан выводить:

1. Что было найдено.
2. Что будет изменено.
3. Какие файлы затронуты.
4. Какие WPF-зависимости удалены.
5. Какие WinUI 3-эквиваленты использованы.
6. Какие риски остались.
7. Как проверить результат.

## Формат ответа после изменения кода

После каждого изменения агент должен писать:

```text
Изменено:
- ...

Почему:
- ...

Проверка:
- ...

Остались риски:
- ...
```

## Нельзя делать

Агенту запрещено:

- переписывать всё приложение за один проход без анализа;
- удалять код, если непонятно, зачем он нужен;
- ломать публичные API внутри проекта без причины;
- менять бизнес-логику вместе с UI-миграцией;
- заменять нативные WinUI-контролы кастомными;
- скрывать ошибки сборки;
- оставлять TODO вместо реализации, если можно реализовать сразу;
- утверждать, что миграция завершена, если приложение не собрано и не проверено.

## Приоритеты

Порядок приоритетов:

1. Стабильная сборка.
2. Сохранение функциональности.
3. Чистое отделение UI от логики.
4. Нативность Windows 11.
5. Поддержка тем.
6. Accessibility.
7. Производительность.
8. Удаление технического долга.

## Финальная проверка

В конце миграции агент обязан выполнить финальный аудит:

- поиск `System.Windows` (кроме `System.Windows.Forms` в `INotifyIconService` реализации);
- поиск `PresentationFramework`;
- поиск `WindowsBase`;
- поиск `DynamicResource`;
- поиск WPF-only controls;
- проверка `.csproj`;
- проверка NuGet-пакетов;
- проверка app manifest;
- проверка Windows App SDK runtime/deployment;
- проверка Light/Dark/High Contrast;
- проверка DPI scaling (100%, 125%, 150%, 200%);
- проверка keyboard-only сценария;
- проверка startup;
- проверка закрытия приложения;
- проверка повторного открытия окон;
- проверка работы всех three drag-and-drop сценариев;
- проверка работы ScreenColorPickerWindow;
- запуск всех unit-тестов.

## Тип деплоя

**Фиксировано**: Unpackaged (самостоятельный exe, без MSIX-пакета).

Преимущества для AiteBar:
- Простота для desktop-приложения с tray
- Легкость интеграции с WinForms NotifyIcon
- Знакомый пользователю опыт установки (инсталлятор)

## Итоговая цель

AiteBar должен стать современным WinUI 3-приложением, которое ощущается как нативная часть Windows 11, а не как WPF-приложение с натянутой темой.
