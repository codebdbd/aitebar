# Инструкция для Codex GPT: SmartScreenDock — исправления v1

## Контекст

Проект: WPF-приложение SmartScreenDock (.NET 8, C#).
Это второй раунд исправлений. Все предыдущие правки уже применены.

Файлы для изменения:
- `SmartScreenDock/MainWindow.xaml.cs`
- `SmartScreenDock/SettingsWindow.xaml.cs`
- `SmartScreenDock/Models.cs`
- Новый файл: `SmartScreenDock/Logger.cs`

---

## Исправление 1 — Вынести LogError в статический класс Logger

**Проблема:** метод `LogError(Exception ex)` продублирован идентично в `MainWindow.xaml.cs` и `SettingsWindow.xaml.cs`. При изменении формата лога нужно менять в двух местах.

### Шаг 1: Создать файл `SmartScreenDock/Logger.cs`

```csharp
using System;
using System.IO;

namespace SmartScreenDock
{
    internal static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Codebdbd", "Aite Deck", "error.log");

        public static void Log(Exception ex)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch { }
        }
    }
}
```

### Шаг 2: В `MainWindow.xaml.cs`

Удалить метод `LogError` целиком:
```csharp
// УДАЛИТЬ весь этот метод:
private void LogError(Exception ex)
{
    try
    {
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Codebdbd", "Aite Deck", "error.log");
        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
    }
    catch { }
}
```

Заменить все вызовы `LogError(ex)` на `Logger.Log(ex)` по всему файлу.

### Шаг 3: В `SettingsWindow.xaml.cs`

Аналогично — удалить метод `LogError` целиком и заменить все вызовы `LogError(ex)` на `Logger.Log(ex)`.

---

## Исправление 2 — Защита BtnCalc_Click от исключения

**Проблема:** единственный обработчик без `try/catch`. На Windows LTSC и в некоторых корпоративных окружениях `calc.exe` может отсутствовать.

### В `MainWindow.xaml.cs` заменить:

```csharp
// БЫЛО:
private void BtnCalc_Click(object sender, RoutedEventArgs e) => Process.Start("calc.exe");
```

```csharp
// СТАЛО:
private void BtnCalc_Click(object sender, RoutedEventArgs e)
{
    try { Process.Start("calc.exe"); }
    catch (Exception ex)
    {
        Logger.Log(ex);
        new DarkDialog($"Не удалось открыть калькулятор:\n{ex.Message}") { Owner = this }.ShowDialog();
    }
}
```

---

## Исправление 3 — Защита SaveElement от параллельных вызовов

**Проблема:** двойное нажатие кнопки «Сохранить» может вызвать `SaveElement` дважды одновременно. Второй вызов обновит запись корректно, но оба запустят `SaveConfig` + `RefreshPanel` параллельно, что приведёт к двойной записи файла и двойному перестроению UI.

### В `MainWindow.xaml.cs`:

**Шаг 1:** добавить поле в класс рядом с другими полями:
```csharp
private bool _isSaving = false;
```

**Шаг 2:** обновить метод `SaveElement`:
```csharp
public async Task SaveElement(CustomElement updated, string? removeId = null)
{
    if (_isSaving) return;
    _isSaving = true;
    try
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
    finally
    {
        _isSaving = false;
    }
}
```

Важно: используй `try/finally` чтобы флаг сбрасывался даже при исключении.

---

## Порядок коммитов

```
refactor: extract LogError into static Logger class
fix: wrap BtnCalc_Click in try/catch
fix: guard SaveElement against concurrent calls with _isSaving flag
```

---

## Ограничения

- Не трогать XAML-файлы.
- Не менять структуру JSON и модель `CustomElement`.
- `Logger.cs` должен быть в том же namespace `SmartScreenDock`.
