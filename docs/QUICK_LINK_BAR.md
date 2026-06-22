# QuickLinkBar: спецификация для реализации в AiteBar

Документ описывает перенос функционала нижней панели QuickLinkBar из AiteProfiles в AiteBar. Цель - дать разработчику достаточную спецификацию для реализации без чтения исходников AiteProfiles.

QuickLinkBar - это компактная нижняя панель быстрого запуска ссылок. Пользователь вводит URL или выбирает сохраненный snippet, закрепляет ссылку при необходимости, включает ротацию профилей браузера и запускает ссылку в выбранных профилях/следующем профиле.

---

## 1. Назначение

QuickLinkBar решает отдельный сценарий, который отличается от обычных кнопок AiteBar:

- быстрый запуск произвольной ссылки без создания постоянной кнопки;
- поиск по сохраненным ссылкам/snippets;
- запуск группы ссылок из одного snippet;
- закрепление текущей ссылки, чтобы она оставалась после запуска;
- ротация браузерных профилей для закрепленной ссылки;
- импорт/экспорт базы snippets;
- редактирование snippets из самой панели.

В AiteBar этот компонент стоит рассматривать как встроенный системный блок панели, а не как пользовательскую кнопку. Он может быть размещен внизу/в конце основной панели рядом с быстрыми утилитами или включаться настройкой `ShowQuickLinkBar`.

---

## 2. UX и состав панели

Панель визуально состоит из одного контейнера с 4 зонами.

| Зона | Элемент | Назначение |
|---|---|---|
| 1 | Поле поиска/ввода | Ввод URL, команды snippet или поискового текста; показ подсказок |
| 2 | Toggle Lock | Закрепить текущую ссылку/snippet после запуска |
| 3 | Toggle Rotation | Запускать закрепленную ссылку в следующем доступном профиле |
| 4 | Menu | Добавить, изменить, импортировать, экспортировать snippets, открыть настройки |
| 5 | Start button | Запустить текущую ссылку/snippet |

В оригинальной реализации AiteProfiles используется WinUI `AutoSuggestBox`. В AiteBar на WPF нужно реализовать аналог одним из способов:

- предпочтительно: кастомный `QuickLinkSuggestBox` на базе `TextBox` + `Popup` + `ListBox`;
- допустимо: editable `ComboBox` с кастомным item template;
- не рекомендуется: отдельное модальное окно поиска, потому что сценарий должен оставаться inline.

### Содержимое подсказки

Каждый item в списке подсказок показывает:

- иконку ссылки;
- `Name` snippet;
- основной URL (`PrimaryUrl`);
- бейджи тегов (`TagsDisplay`), если теги есть.

### Индикаторы состояния в поле

Внутри правой части поля отображаются маленькие индикаторы:

- Lock indicator - виден, когда текущая ссылка закреплена;
- Rotation indicator - виден, когда включена ротация.

Индикаторы только показывают состояние и не должны перехватывать клики.

### Основная кнопка запуска

Текст кнопки меняется от состояния:

| Состояние | Текст | Tooltip |
|---|---|---|
| Обычный запуск | `Запуск` | Запустить ссылку в выбранном профиле |
| Выбрано несколько профилей | `Запуск (N)` | Запустить ссылку в N выбранных профилях |
| Включена ротация | `Следующий слот` | Запустить ссылку в следующем профиле ротации |

Для AiteBar текст можно локализовать через `LocalizationService`.

---

## 3. Пользовательские сценарии

### 3.1 Ввод прямого URL

1. Пользователь вводит `example.com` или `https://example.com`.
2. Сервис нормализует адрес до абсолютного `https://example.com/`.
3. При нажатии Enter или Start создается временный snippet.
4. Ссылка запускается через существующую браузерную инфраструктуру AiteBar.
5. Если Lock выключен, поле очищается после запуска.

### 3.2 Выбор сохраненного snippet

1. Пользователь начинает вводить имя, тег или часть URL.
2. Список подсказок ранжируется: сначала совпадения по тегам, потом по имени, потом по URL.
3. При выборе item поле получает `PrimaryUrl`, а активный snippet сохраняется в сервисе состояния.
4. Start запускает все URL из выбранного snippet.

### 3.3 Создание snippet строкой-командой

Поддержать формат:

```text
tag1,tag2:Name:https://site1.com|https://site2.com
```

Правила:

- до первого `:` - список тегов;
- между первым и вторым `:` - название;
- после второго `:` - один или несколько URL через `|`;
- строка валидна только если есть хотя бы один тег, непустое имя и хотя бы один валидный URL.

Если пользователь отправляет такую строку, snippet сохраняется в базу.

### 3.4 Lock

Lock фиксирует текущий snippet/URL как активный.

При включении Lock:

1. Текущий текст валидируется как выбранный snippet, command-format snippet или direct URL.
2. Если текст невалиден, Lock откатывается в `false`, поле очищается, показывается сообщение.
3. Если текст валиден, input блокируется, чтобы пользователь случайно не изменил закрепленную ссылку.
4. Кнопка Lock получает активный фон и tooltip `Снять закрепление ссылки`.
5. Rotation становится доступной.

При выключении Lock:

1. Активный snippet очищается.
2. Подготовленный текст очищается.
3. Input снова включается.
4. Rotation выключается и становится недоступной.

### 3.5 Rotation

Rotation доступна только при включенном Lock. Это важное ограничение: ротация должна запускать одну и ту же закрепленную ссылку по очереди в разные браузерные профили.

При включении Rotation:

- `QuickLinkBar.IsRotationChecked = true`;
- состояние передается в логику запуска;
- кнопка запуска меняет текст на `Следующий слот`;
- следующий запуск выбирает следующий профиль через существующий механизм AiteBar (`ProfileRotationHelper`, `BrowserHelper.GetProfiles`, `CustomElement.LastUsedProfile` или отдельный state для QuickLink).

Если Lock выключается, Rotation обязательно сбрасывается.

### 3.6 Import

Импорт поддерживает `.txt` и `.json`.

TXT импортирует только строки command-формата:

```text
tag:Name:https://example.com
work,mail:Gmail:https://mail.google.com
social:Group:https://x.com|https://linkedin.com
```

JSON импортирует массив объектов модели `QuickLinkSnippet`:

```json
[
  {
    "name": "Gmail",
    "urls": ["https://mail.google.com/"],
    "tags": ["mail", "work"]
  }
]
```

Минимальная версия может импортировать только TXT, но экспорт JSON уже должен быть заложен в модель, чтобы не менять контракт позже.

### 3.7 Export

Экспорт поддерживает:

- `.txt` - строки `firstTag:Name:url1|url2`;
- `.json` - нормализованный массив `QuickLinkSnippet`.

### 3.8 Add/Edit

Add открывает диалог создания snippet.

Edit пытается найти текущий snippet так:

1. выбранная подсказка;
2. snippet, полученный из текущего текста;
3. активный закрепленный snippet.

Если snippet не найден, показывается сообщение `Выберите или введите quick link для редактирования`.

---

## 4. Модель данных

Добавить модель, например `AiteBar/QuickLinkSnippet.cs`:

```csharp
public sealed class QuickLinkSnippet
{
    public string Name { get; set; } = string.Empty;
    public List<string> Urls { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}
```

Добавить view-model item для подсказок:

```csharp
public sealed class QuickLinkSuggestionItem
{
    public QuickLinkSuggestionItem(QuickLinkSnippet snippet)
    {
        Snippet = snippet;
    }

    public QuickLinkSnippet Snippet { get; }
    public string Name => Snippet.Name;
    public string PrimaryUrl => Snippet.Urls.FirstOrDefault() ?? string.Empty;
    public string TagsDisplay => string.Join(", ", Snippet.Tags);

    public override string ToString() => PrimaryUrl;
}
```

---

## 5. Файлы хранения

Рекомендуемые пути через существующий `PathHelper`/app data AiteBar:

| Файл | Назначение |
|---|---|
| `quick_links.json` | База snippets |
| `quick_link_last_launch.json` | Последний запущенный snippet/text |
| `quick_link_state.json` или settings field | Опционально: remember/rotation state между сессиями |

Если `AppSettings` уже является главным источником persistent UI state, можно добавить туда:

```csharp
public bool ShowQuickLinkBar { get; set; } = true;
public bool QuickLinkRemember { get; set; }
public bool QuickLinkRotation { get; set; }
public BrowserType QuickLinkBrowser { get; set; } = BrowserType.Chrome;
public string QuickLinkProfile { get; set; } = string.Empty;
public List<string> QuickLinkRotationProfilePaths { get; set; } = [];
public string QuickLinkLastUsedProfile { get; set; } = string.Empty;
```

Важно: если используются поля `QuickLinkRotationProfilePaths` и `QuickLinkLastUsedProfile`, их нужно включить в clone/normalize/equality логику `AppSettingsService`.

---

## 6. Сервисы

### 6.1 QuickLinkSnippetService

Ответственность:

- загрузка/сохранение `quick_links.json`;
- нормализация snippets;
- парсинг command-строк;
- парсинг прямых URL;
- импорт/экспорт TXT/JSON;
- валидация URL.

Публичный контракт:

```csharp
public sealed class QuickLinkSnippetService
{
    public Task<IReadOnlyList<QuickLinkSnippet>> LoadAsync();
    public Task SaveAsync(IReadOnlyList<QuickLinkSnippet> snippets);

    public bool TryParseCommand(string input, out QuickLinkSnippet snippet);
    public bool TryParseDirectUrls(string input, out List<string> urls);
    public bool TryNormalizeUrl(string rawInput, out string normalizedUrl);

    public IReadOnlyList<QuickLinkSnippet> ParseImportText(string content);
    public IReadOnlyList<QuickLinkSnippet> ParseImportJson(string json);
    public string BuildTextExport(IReadOnlyList<QuickLinkSnippet> snippets);
    public string BuildJsonExport(IReadOnlyList<QuickLinkSnippet> snippets);

    public static IReadOnlyList<QuickLinkSnippet> NormalizeSnippets(IEnumerable<QuickLinkSnippet> snippets);
}
```

Нормализация:

- trim для name/tags/urls;
- tags приводить к lower-case;
- пустые tags заменять на `misc`;
- URL без схемы дополнять `https://`;
- разрешать только `http` и `https`;
- разрешать host с точкой, `localhost` и IP;
- удалять дубликаты по ключу `Name|url1|url2`;
- сортировать по `Name` без учета регистра.

### 6.2 QuickLinkSelectionService

Ответственность:

- хранить активный snippet текущей сессии;
- хранить prepared text из input;
- знать, включен ли Lock;
- сохранять последний запуск;
- возвращать текст, который должен остаться в поле после запуска.

Публичный контракт:

```csharp
public sealed class QuickLinkSelectionService
{
    public QuickLinkSnippet? GetActiveSnippet();
    public void SetActiveSnippet(QuickLinkSnippet snippet);
    public void Clear();

    public void UpdatePreparedText(string preparedText);
    public string GetPreparedText();

    public void SetRememberEnabled(bool rememberEnabled);
    public bool GetRememberEnabled();

    public string MarkLaunched(QuickLinkSnippet snippet);
    public QuickLinkSnippet? GetLastLaunchedSnippet();
    public string GetLastLaunchedText();
}
```

`MarkLaunched` должен:

- сохранить последний запущенный snippet в файл;
- если Lock включен, вернуть prepared text, чтобы поле осталось заполненным;
- если Lock выключен, очистить prepared text и active snippet, вернуть пустую строку.

### 6.3 QuickLinkViewModel

Ответственность:

- лениво загружать snippets;
- поддерживать `ObservableCollection<QuickLinkSuggestionItem>`;
- ранжировать подсказки;
- обрабатывать Submit, Import, Export, Add/Edit;
- сообщать UI об ошибках через event/dialog callback.

Ключевые свойства:

```csharp
public ObservableCollection<QuickLinkSuggestionItem> Suggestions { get; }
public string InputText { get; set; }
public bool Remember { get; set; }
public QuickLinkSuggestionItem? SelectedSuggestion { get; set; }
```

Ключевые методы:

```csharp
public Task EnsureLoadedAsync();
public Task RefreshAsync();
public void UpdateSuggestions(string query);
public void ChooseSuggestion(object? selected);
public Task SubmitAsync(string? query, object? chosenSuggestion);
public Task<bool> SetLockStateAsync(bool lockEnabled, string currentInput);
public Task ImportTextAsync(string content);
public string BuildExportText();
public string BuildExportJson();
public bool TryResolveCurrentSnippetForEdit(string currentInput, out QuickLinkSnippet snippet);
public Task SaveSnippetFromEditorAsync(QuickLinkSnippet edited, QuickLinkSnippet? original = null);
```

Ранжирование подсказок:

1. tag contains query - rank 0;
2. name contains query - rank 1;
3. url contains query - rank 2;
4. сортировка внутри rank по name;
5. максимум 50 подсказок.

---

## 7. Запуск ссылок в AiteBar

QuickLinkBar не должен самостоятельно строить `ProcessStartInfo`. Он должен использовать существующий механизм AiteBar для web-запуска.

Рекомендуемый подход: добавить метод в `ActionService`:

```csharp
public async Task<ActionExecutionResult> LaunchQuickLinkAsync(
    QuickLinkSnippet snippet,
    QuickLinkLaunchOptions options,
    Func<Task>? onBeforeExecute = null)
```

Модель options:

```csharp
public sealed class QuickLinkLaunchOptions
{
    public BrowserType Browser { get; set; } = BrowserType.Chrome;
    public string Profile { get; set; } = string.Empty;
    public bool UseRotation { get; set; }
    public List<string> RotationProfilePaths { get; set; } = [];
    public bool Incognito { get; set; }
    public bool AppMode { get; set; }
    public bool Fullscreen { get; set; }
}
```

Внутри `LaunchQuickLinkAsync` можно создать временный `CustomElement`:

```csharp
var element = new CustomElement
{
    Name = snippet.Name,
    ActionType = ActionType.Web,
    Url = string.Join("|", snippet.Urls),
    Browser = options.Browser,
    ChromeProfile = options.Profile,
    RotationProfilePaths = [.. options.RotationProfilePaths],
    UseRotation = options.UseRotation,
    Incognito = options.Incognito,
    AppMode = options.AppMode,
    Fullscreen = options.Fullscreen,
};
```

Дальше переиспользовать существующую web-логику `ActionService`. Если текущий `ActionService` не поддерживает несколько URL в одном `CustomElement.Url`, нужно либо:

- добавить запуск каждого URL по очереди в выбранном профиле;
- либо хранить group snippet как список и вызывать web launch для каждого URL.

После успешного запуска:

1. вызвать `QuickLinkSelectionService.MarkLaunched(snippet)`;
2. обновить поле input возвращенным текстом;
3. сохранить `QuickLinkLastUsedProfile`, если использовалась rotation;
4. скрыть панель AiteBar, если поведение обычных кнопок также скрывает панель после запуска.

---

## 8. WPF UserControl

Создать `AiteBar/QuickLinkBar.xaml` и `AiteBar/QuickLinkBar.xaml.cs`.

### Публичные свойства control

```csharp
public IEnumerable? SuggestionsSource { get; set; }
public string InputText { get; set; }
public bool IsInputEnabled { get; set; }
public bool IsInputFocused { get; }

public bool IsRememberChecked { get; set; }
public bool IsRememberEnabled { get; set; }
public bool IsRotationChecked { get; set; }
public bool IsRotationEnabled { get; set; }
```

### События control

```csharp
public event KeyEventHandler? InputKeyDown;
public event EventHandler<QuickLinkQuerySubmittedEventArgs>? QuerySubmitted;
public event EventHandler<QuickLinkSuggestionChosenEventArgs>? SuggestionChosen;
public event EventHandler<TextChangedEventArgs>? InputTextChanged;
public event RoutedEventHandler? RememberChanged;
public event RoutedEventHandler? RotationChanged;
public event RoutedEventHandler? AddClicked;
public event RoutedEventHandler? EditClicked;
public event RoutedEventHandler? ImportClicked;
public event RoutedEventHandler? ExportClicked;
public event RoutedEventHandler? SettingsClicked;
public event RoutedEventHandler? StartClicked;
```

### Методы control

```csharp
public void SetStartButtonText(string text, string? tooltip = null);
public bool FocusInput();
public void SetRememberVisual(string glyphOrIconKey, Brush background, string tooltip);
public void SetRotationVisual(Brush background, string tooltip);
```

Control должен быть тонким: он не читает файлы, не запускает браузер и не знает бизнес-правила. Он только отображает состояние и прокидывает события в `MainWindow`/view-model.

---

## 9. Интеграция с MainWindow

В `MainWindow.xaml` добавить control в визуальную структуру панели. Точное место зависит от текущего layout AiteBar, но логически это нижний/последний блок после пользовательских кнопок и системных утилит.

В `MainWindow.xaml.cs` добавить поля:

```csharp
private QuickLinkViewModel? _quickLinkViewModel;
private Task? _quickLinkLoadTask;
```

На инициализации окна:

```csharp
QuickLinkBar.InputKeyDown += QuickLinkInputKeyDown;
QuickLinkBar.QuerySubmitted += QuickLinkQuerySubmitted;
QuickLinkBar.SuggestionChosen += QuickLinkSuggestionChosen;
QuickLinkBar.InputTextChanged += QuickLinkTextChanged;
QuickLinkBar.RememberChanged += QuickLinkRememberChanged;
QuickLinkBar.RotationChanged += QuickLinkRotationChanged;
QuickLinkBar.AddClicked += QuickLinkAddClicked;
QuickLinkBar.EditClicked += QuickLinkEditClicked;
QuickLinkBar.ImportClicked += QuickLinkImportClicked;
QuickLinkBar.ExportClicked += QuickLinkExportClicked;
QuickLinkBar.SettingsClicked += OpenAppSettings;
QuickLinkBar.StartClicked += QuickLinkStartClicked;
```

На закрытии окна обязательно отписаться от событий.

### Lazy load

Snippets нужно грузить лениво при первом взаимодействии:

```csharp
private bool EnsureQuickLinkInitialized()
{
    if (_quickLinkViewModel != null)
        return true;

    _quickLinkViewModel = new QuickLinkViewModel(_quickLinkSnippetService, _quickLinkSelectionService);
    QuickLinkBar.SuggestionsSource = _quickLinkViewModel.Suggestions;
    QuickLinkBar.IsRememberChecked = _quickLinkViewModel.Remember;
    QuickLinkBar.IsInputEnabled = !_quickLinkViewModel.Remember;
    SyncQuickLinkRotationAvailability(_quickLinkViewModel.Remember);
    return true;
}
```

---

## 10. Горячие клавиши и фокус

Минимальные shortcuts:

| Shortcut | Действие |
|---|---|
| `Ctrl+L` | Фокус в поле QuickLinkBar |
| `Enter` в поле | Submit/Start current quick link |
| `Shift+Ctrl+V` в поле | Импорт snippets из clipboard text |
| `Esc` | Если фокус в поле - убрать popup/фокус; иначе обычное поведение панели |

`Shift+Ctrl+V` должен читать clipboard text и передавать его в `QuickLinkViewModel.ImportTextAsync`.

---

## 11. Локализация

Добавить ключи во все resource-файлы AiteBar: `Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, `Strings.de.resx`.

Рекомендуемые ключи:

```text
QuickLink_Placeholder
QuickLink_Status_Locked
QuickLink_Status_Rotation
QuickLink_Tooltip_Lock
QuickLink_Tooltip_Rotation
QuickLink_Tooltip_Manage
QuickLink_Menu_Add
QuickLink_Menu_Edit
QuickLink_Menu_Import
QuickLink_Menu_Export
QuickLink_Menu_Settings
QuickLink_Start
QuickLink_StartTooltip
QuickLink_StartTooltipMulti
QuickLink_StartTextMulti
QuickLink_StartNextSlot
QuickLink_StartNextSlotTooltip
QuickLink_Unlock
QuickLink_Lock
QuickLink_RotationEnable
QuickLink_RotationDisable
QuickLink_RotationLockRequired
QuickLink_InvalidBeforeLock
QuickLink_EditRequired
QuickLink_Title
QuickLink_Direct
QuickLink_Group
```

Тест `LocalizationServiceTests.ResourceFiles_HaveSameKeysAndFormatPlaceholders` должен проходить после добавления ключей.

---

## 12. Настройки

Добавить в `AppSettingsWindow`:

- checkbox `Показывать QuickLinkBar`;
- выбор браузера по умолчанию для QuickLink;
- выбор профиля по умолчанию;
- checkbox `Incognito/Private` при необходимости;
- кнопка выбора profiles для rotation, используя существующий `RotationProfileSelectionWindow`.

Если нужен минимальный MVP, можно сначала использовать системный default browser/profile и не добавлять отдельный UI настроек профиля. Но `ShowQuickLinkBar` должен быть с самого начала, чтобы пользователь мог отключить блок.

---

## 13. Ошибки и edge cases

Обработать обязательно:

- пустой input при Start - ничего не делать;
- невалидный URL при Lock - откатить Lock и показать сообщение;
- импорт файла без валидных строк - ничего не менять;
- битый `quick_links.json` - залогировать warning и вернуть пустой список;
- отсутствующий браузер - использовать существующее сообщение `Action_BrowserNotFound`;
- rotation включена, но профилей нет - показать ошибку или выключить rotation;
- snippet с несколькими URL - запускать все URL предсказуемо в одном выбранном профиле;
- popup подсказок не должен перекрывать основную панель так, чтобы терялся hover/focus сценарий AiteBar;
- при скрытии панели popup подсказок должен закрываться.

---

## 14. Тесты

Добавить unit-тесты минимум для сервисного слоя:

### QuickLinkSnippetServiceTests

- `TryNormalizeUrl_AddsHttps`;
- `TryNormalizeUrl_RejectsInvalidHost`;
- `TryParseCommand_ParsesTagsNameAndUrls`;
- `ParseImportText_SkipsMalformedLines`;
- `NormalizeSnippets_DeduplicatesAndSorts`;
- `BuildTextExport_UsesFirstTagAndPipeSeparatedUrls`;
- `BuildJsonExport_RoundTrips`.

### QuickLinkSelectionServiceTests

- `MarkLaunched_WhenRememberDisabled_ClearsPreparedText`;
- `MarkLaunched_WhenRememberEnabled_KeepsPreparedText`;
- `GetLastLaunchedText_ReadsPersistedRecord`;
- `Clear_RemovesActiveSnippetOnly`.

### QuickLinkViewModelTests

- `UpdateSuggestions_RanksTagBeforeNameBeforeUrl`;
- `SubmitAsync_DirectUrlCreatesTemporarySnippet`;
- `SubmitAsync_CommandSnippetPersistsSnippet`;
- `SetLockStateAsync_InvalidInputReturnsFalse`;
- `TryResolveCurrentSnippetForEdit_UsesSelectedSuggestionFirst`.

### Integration/manual checks

- `Ctrl+L` focuses input;
- `Shift+Ctrl+V` imports clipboard snippets;
- Lock disables input and enables Rotation;
- disabling Lock disables Rotation;
- Start launches in selected profile;
- Start with Rotation advances profile;
- Export TXT/JSON creates valid files;
- localization keys present in all languages.

---

## 15. Минимальная последовательность реализации

1. Добавить `QuickLinkSnippet`, `QuickLinkSuggestionItem`.
2. Добавить `QuickLinkSnippetService` с JSON/TXT parsing, normalization и tests.
3. Добавить `QuickLinkSelectionService` и tests.
4. Добавить `QuickLinkViewModel` с ranking, submit, lock, import/export.
5. Добавить WPF `QuickLinkBar` control без бизнес-логики.
6. Встроить control в `MainWindow` и подписать события.
7. Добавить `ActionService.LaunchQuickLinkAsync` или adapter через временный `CustomElement`.
8. Подключить launch-result к `QuickLinkSelectionService.MarkLaunched`.
9. Добавить localization keys во все `Strings*.resx`.
10. Добавить setting `ShowQuickLinkBar` и UI в `AppSettingsWindow`.
11. Добавить shortcuts `Ctrl+L` и `Shift+Ctrl+V`.
12. Прогнать unit-тесты и ручную проверку launch/lock/rotation/import/export.

---

## 16. Отличия от AiteProfiles, которые нужно учесть

- AiteProfiles использует WinUI `AutoSuggestBox`; AiteBar использует WPF, поэтому нужен WPF-аналог.
- В AiteProfiles панель запускает ссылки в выбранных профилях из таблицы профилей. В AiteBar нет такой таблицы на главном экране, поэтому профиль нужно брать из настроек QuickLink или из существующей browser/profile настройки.
- В AiteProfiles Rotation состояние связано с `MainViewModel.RotationEnabled`. В AiteBar лучше хранить QuickLink rotation отдельно, чтобы не смешивать его с rotation пользовательских кнопок.
- В AiteProfiles snippets лежат в WinRT `ApplicationData.Current.LocalFolder` с fallback. В AiteBar нужно использовать существующий путь данных приложения через `PathHelper`/`AppSettingsService`.
- В AiteProfiles control называется `QuickLinkBar`, но связанная логика в коде местами называется `Terminal`. В AiteBar лучше использовать единый нейминг `QuickLink*`.

---

## 17. Acceptance criteria

Функция считается реализованной, если:

- пользователь видит QuickLinkBar на панели и может отключить его в настройках;
- ввод `example.com` и Enter запускает `https://example.com/`;
- сохраненные snippets появляются в подсказках и фильтруются по tag/name/url;
- command string `tag:Name:url` создает/обновляет snippet;
- Lock закрепляет валидную ссылку и блокирует input;
- Rotation доступна только при Lock и запускает следующий профиль;
- Add/Edit открывают диалог и сохраняют изменения;
- Import/Export работают для TXT, JSON не ломает контракт;
- `Ctrl+L` и `Shift+Ctrl+V` работают;
- ошибки показываются пользователю без падения приложения;
- unit-тесты сервисов и локализационный тест проходят.
