# Инструкция для Codex GPT: SmartScreenDock — исправления перед релизом

## Контекст

Проект: WPF-приложение SmartScreenDock (.NET 8, C#).
Структура исходников:
- `SmartScreenDock/MainWindow.xaml.cs`
- `SmartScreenDock/SettingsWindow.xaml.cs`
- `SmartScreenDock/Models.cs`
- `SmartScreenDock/IconPickerWindow.xaml.cs`
- `SmartScreenDock/DarkDialog.xaml.cs`
- `SmartScreenDock/App.xaml.cs`

Задача: последовательно применить все исправления, описанные ниже. Каждое исправление — отдельный коммит с понятным сообщением. Не трогай XAML-разметку, если изменение не указано явно.

---

## ЭТАП 1 — Критические исправления

### 1.1 Устранить Race Condition при сохранении конфига

**Файлы:** `SettingsWindow.xaml.cs`, `MainWindow.xaml.cs`

**Проблема:** `SettingsWindow.BtnSave_Click` самостоятельно читает файл с диска и пишет его обратно. `MainWindow.SaveConfig` пишет из `_elements` в памяти. При одновременном вызове данные перезаписываются.

**Что сделать:**

1. В `MainWindow.xaml.cs` добавить публичный метод:
```csharp
public async Task SaveElement(CustomElement updated, string? removeId = null)
{
    if (removeId != null)
        _elements.RemoveAll(x => x.Id == removeId);

    var existing = _elements.FirstOrDefault(x => x.Id == updated.Id);
    if (existing != null)
        _elements[_elements.IndexOf(existing)] = updated;
    else
        _elements.Add(updated);

    await SaveConfig();
    await RefreshPanel();
}
```

2. В `SettingsWindow.BtnSave_Click` — убрать весь код чтения/записи файла (`File.ReadAllTextAsync`, `JsonSerializer`, `File.WriteAllTextAsync`). Вместо этого вызвать:
```csharp
await _mainWindow.SaveElement(newElement, _editingElement?.Id);
```

3. Убедиться, что `SaveConfig` в `MainWindow` остаётся `private`.

---

### 1.2 Обернуть все `async void` обработчики в `try/catch`

**Файлы:** `MainWindow.xaml.cs`, `SettingsWindow.xaml.cs`

**Проблема:** необработанное исключение в `async void` крашит приложение без сообщения.

**Что сделать:** обернуть тело каждого `async void` обработчика в `try/catch`. При ошибке показывать `DarkDialog` (не `MessageBox`). Шаблон:

```csharp
private async void BtnSave_Click(object sender, RoutedEventArgs e)
{
    try
    {
        // ... существующий код ...
    }
    catch (Exception ex)
    {
        new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog();
    }
}
```

Применить к: `BtnSave_Click`, `Window_Loaded`, `BtnSearch_Click`, и всем остальным `async void` в обоих файлах.

---

### 1.3 Безопасный парсинг цвета через BrushConverter

**Файлы:** `MainWindow.xaml.cs`, `SettingsWindow.xaml.cs`

**Проблема:** `(Brush)new BrushConverter().ConvertFromString(el.Color)!` выбросит `NullReferenceException` при невалидном цвете в JSON.

**Что сделать:** заменить все вхождения на:
```csharp
new BrushConverter().ConvertFromString(el.Color) as Brush ?? Brushes.White
```

Найти все места где используется `BrushConverter().ConvertFromString` с `!` или жёстким кастом — заменить все.

---

### 1.4 Подтверждение перед выполнением консольной команды

**Файл:** `MainWindow.xaml.cs`, метод `ExecuteCustomAction`

**Проблема:** команда из поля `ActionValue` передаётся в `cmd.exe /c` без какого-либо предупреждения пользователю.

**Что сделать:** перед `Process.Start` для `ActionType == "Command"` показать `DarkDialog` с подтверждением:

```csharp
else if (el.ActionType == "Command")
{
    var confirm = new DarkDialog($"Будет выполнена команда:\n\n{el.ActionValue}\n\nПродолжить?", isConfirm: true);
    confirm.Owner = Application.Current.MainWindow;
    if (confirm.ShowDialog() != true) return;
    Process.Start(new ProcessStartInfo("cmd.exe", $"/c {el.ActionValue}")
        { CreateNoWindow = true, UseShellExecute = false });
}
```

---

## ЭТАП 2 — Надёжность логики

### 2.1 Надёжный TopMost для Chrome через polling

**Файл:** `MainWindow.xaml.cs`, метод `ExecuteCustomAction`

**Проблема:** `await Task.Delay(1000)` не гарантирует появление окна. `MainWindowHandle` может быть `IntPtr.Zero`.

**Что сделать:** заменить блок с `Task.Delay(1000)` на:

```csharp
if (el.IsTopmost && proc != null)
{
    for (int i = 0; i < 25; i++)
    {
        await Task.Delay(200);
        proc.Refresh();
        if (proc.MainWindowHandle != IntPtr.Zero)
        {
            SetWindowPos(proc.MainWindowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
            break;
        }
    }
}
```

---

### 2.2 Парсить Chrome Preferences через JsonDocument

**Файл:** `SettingsWindow.xaml.cs`, метод `LoadChromeProfiles`

**Проблема:** `File.ReadAllText` + `Regex` — читает весь файл в память (3–10 МБ), регулярка ломается на вложенных объектах.

**Что сделать:** заменить блок чтения имени профиля на:

```csharp
if (File.Exists(prefFile))
{
    try
    {
        using var stream = File.OpenRead(prefFile);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        // Пробуем account_info[0].email
        if (root.TryGetProperty("account_info", out var accounts) &&
            accounts.ValueKind == JsonValueKind.Array &&
            accounts.GetArrayLength() > 0)
        {
            var first = accounts[0];
            if (first.TryGetProperty("email", out var emailProp) &&
                !string.IsNullOrWhiteSpace(emailProp.GetString()))
            {
                displayName = emailProp.GetString()!;
            }
        }
        // Fallback: profile.name
        else if (root.TryGetProperty("profile", out var profile) &&
                 profile.TryGetProperty("name", out var nameProp))
        {
            displayName = nameProp.GetString() ?? displayName;
        }
    }
    catch { /* оставляем displayName = имя папки */ }
}
```

Убрать `using System.Text.RegularExpressions;` если он больше нигде не используется.

---

### 2.3 Убрать скрытый side-effect из GetNextProfile

**Файл:** `MainWindow.xaml.cs`

**Проблема:** метод `GetNextProfile` неожиданно мутирует `el.LastUsedProfile`.

**Что сделать:**

1. Переименовать `GetNextProfile` в `AdvanceProfile`.
2. Метод только вычисляет и возвращает следующий профиль — без `await SaveConfig()` внутри.
3. В `ExecuteCustomAction` явно вызвать `SaveConfig()` после:
```csharp
if (el.UseRotation)
{
    prof = AdvanceProfile(el);
    await SaveConfig();
}
```

---

## ЭТАП 3 — Оптимизация UI

### 3.1 Устранить фриз при загрузке иконок

**Файл:** `IconPickerWindow.xaml.cs`, метод `LoadAllIcons`

**Проблема:** 672 кнопки создаются синхронно на UI-потоке при открытии окна — заметный фриз.

**Что сделать:** разбить генерацию на порции через `Dispatcher.InvokeAsync`:

```csharp
private async void LoadAllIcons()
{
    Style btnStyle = (Style)FindResource("IconBtnStyle");
    int[] codes = Enumerable.Range(0xE700, 0xE9A0 - 0xE700 + 1).ToArray();

    const int batchSize = 50;
    for (int batch = 0; batch < codes.Length; batch += batchSize)
    {
        int end = Math.Min(batch + batchSize, codes.Length);
        for (int i = batch; i < end; i++)
        {
            int code = codes[i];
            var btn = new Button
            {
                Content = char.ConvertFromUtf32(code),
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 24, Width = 46, Height = 46, Margin = new Thickness(2),
                Style = btnStyle, ToolTip = $"U+{code:X4}",
                Background = Brushes.Transparent, Foreground = Brushes.White,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            btn.Click += (s, e) =>
            {
                SelectedIcon = char.ConvertFromUtf32(code);
                this.DialogResult = true;
            };
            IconPanel.Children.Add(btn);
        }
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
    }
}
```

Изменить сигнатуру метода с `private void` на `private async void` и вызвать его из конструктора как раньше.

---

### 3.2 Заменить все MessageBox на DarkDialog

**Файлы:** `MainWindow.xaml.cs`, `SettingsWindow.xaml.cs`

**Проблема:** стандартный `MessageBox.Show` выглядит инородно в тёмном интерфейсе. `DarkDialog` уже реализован, но не используется.

**Что сделать:** найти все вызовы `MessageBox.Show(...)` и заменить:

- Для информационных сообщений:
```csharp
new DarkDialog("Текст сообщения") { Owner = this }.ShowDialog();
```

- Для подтверждений (где был `MessageBoxButton.YesNo`):
```csharp
var dlg = new DarkDialog("Вопрос?", isConfirm: true) { Owner = this };
if (dlg.ShowDialog() == true) { /* действие */ }
```

Убрать `using MessageBox = System.Windows.MessageBox;` после замены, если алиас больше не нужен.

---

## ЭТАП 4 — Рефакторинг и чистота кода

### 4.1 Добавить логирование ошибок

**Файлы:** `MainWindow.xaml.cs`, `SettingsWindow.xaml.cs`

**Проблема:** пустые `catch { }` молча проглатывают ошибки.

**Что сделать:** в `MainWindow` добавить вспомогательный метод:

```csharp
private void LogError(Exception ex)
{
    try
    {
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Codebdbd", "Aite Deck", "error.log");
        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
    }
    catch { /* логирование упало — игнорируем */ }
}
```

Заменить все пустые `catch { }` и `catch { _elements = new(); }` на:
```csharp
catch (Exception ex) { LogError(ex); _elements = new(); }
```

---

### 4.2 Заменить магические числа BlockId на enum

**Файл:** `Models.cs`

**Что сделать:** добавить enum:

```csharp
public enum DockBlock
{
    Utils = 1,
    AI = 2,
    Web = 3,
    Scripts = 4,
    Other = 5
}
```

В `Models.cs` свойство `BlockId` оставить типом `int` (для совместимости с сохранённым JSON). В `MainWindow.xaml.cs` в `switch` заменить числа:

```csharp
switch ((DockBlock)el.BlockId)
{
    case DockBlock.Utils:   Block1_Utils.Children.Add(btn); break;
    case DockBlock.AI:      Block2_AI.Children.Add(btn); break;
    case DockBlock.Web:     Block3_Web.Children.Add(btn); break;
    case DockBlock.Scripts: Block4_Scripts.Children.Add(btn); break;
    case DockBlock.Other:   Block5_Other.Children.Add(btn); break;
}
```

В `SettingsWindow.xaml.cs` при парсинге `CmbBlock` оставить `int.Parse` — совместимость с XAML-тегами `Tag="1"` и т.д.

---

### 4.3 Улучшить GetChromePath через реестр

**Файл:** `MainWindow.xaml.cs`, метод `GetChromePath`

**Что сделать:** добавить проверку реестра первой в методе:

```csharp
private string GetChromePath()
{
    // Сначала проверяем реестр
    try
    {
        var regVal = Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
            "", null) as string;
        if (!string.IsNullOrEmpty(regVal) && File.Exists(regVal))
            return regVal;
    }
    catch { }

    // Fallback: стандартные пути
    string[] paths = {
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Google\Chrome\Application\chrome.exe")
    };
    foreach (var p in paths)
        if (File.Exists(p)) return p;

    return "chrome.exe";
}
```

---

### 4.4 Оптимизировать таймер во время анимации

**Файл:** `MainWindow.xaml.cs`, метод `Toggle`

**Что сделать:** остановить таймер на время анимации:

```csharp
private void Toggle(double targetY)
{
    _isAnimating = true;
    _timer.Stop(); // остановить опрос курсора на время анимации

    double finalY = targetY < 0 ? -this.ActualHeight : 0;
    var anim = new DoubleAnimation(finalY, TimeSpan.FromMilliseconds(200))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
    anim.Completed += (s, ev) =>
    {
        this.BeginAnimation(TopProperty, null);
        this.Top = finalY;
        _isAnimating = false;
        _timer.Start(); // возобновить после анимации
    };
    this.BeginAnimation(TopProperty, anim);
}
```

---

## ЭТАП 5 — Сборка и релиз

### 5.1 Собрать в конфигурации Release

Текущий архив содержит `bin/Debug/`. Перед публикацией:

```bash
dotnet publish SmartScreenDock/SmartScreenDock.csproj \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -o ./publish
```

### 5.2 Убрать bin/ и obj/ из архива

В `.gitignore` должно быть:
```
bin/
obj/
*.user
.vs/
```

Финальный релизный архив формировать только из папки `publish/` после `dotnet publish`.

---

## Порядок коммитов

```
fix: eliminate race condition in config save (1.1)
fix: wrap async void handlers in try/catch (1.2)
fix: safe brush parsing with null fallback (1.3)
fix: confirm dialog before executing Command action (1.4)
fix: reliable topmost window via polling (2.1)
fix: parse Chrome Preferences via JsonDocument (2.2)
refactor: rename GetNextProfile to AdvanceProfile, remove side effects (2.3)
perf: load icon picker in batches to prevent UI freeze (3.1)
ui: replace all MessageBox with DarkDialog (3.2)
fix: add error logging to catch blocks (4.1)
refactor: replace BlockId magic numbers with DockBlock enum (4.2)
fix: check registry for Chrome path before hardcoded paths (4.3)
perf: stop timer during dock animation (4.4)
build: switch to Release config, exclude bin/ obj/ from archive (5.1, 5.2)
```

---

## Важные ограничения

- **Не трогать XAML-файлы** без явной необходимости — все изменения касаются только `.cs` файлов.
- **Не менять структуру JSON** в `custom_buttons.json` — пользователи обновятся с v0, обратная совместимость обязательна.
- **`BlockId` оставить `int`** в модели — иначе сломается десериализация существующих конфигов.
- **`DarkDialog`** принимает `Owner = this` — всегда устанавливай, чтобы диалог открывался по центру родительского окна.
