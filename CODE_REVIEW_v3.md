# Code Review AiteBar v3 — Что осталось исправить

Дата: 2026-06-21
Предыдущие ревью: v1.11, v2
Статус: 519/519 тестов ✅

---

## Уже исправлено (не требует действий)

| # | Что | Коммит/файл |
|---|-----|-------------|
| CR-20 | TimerStopwatch: Stopwatch вместо DateTime.UtcNow | ✅ Уже используется `Stopwatch.GetTimestamp()` (`TimerStopwatchWindow.xaml.cs:355`) |
| CR-23 | Quick Note SaveGeometryNowAsync: UpdateSettings | ✅ Уже использует `_settingsService.UpdateSettings()` (`QuickNoteWindow.xaml.cs:860`) |

---

## Осталось исправить

### CR-11: Compact mode таймера — Topmost не сбрасывается [СРЕДНЯЯ]

**Файл:** `AiteBar/TimerStopwatchWindow.xaml.cs:462-465`

**Проблема:** При входе в compact mode ставится `Topmost = true`, но при выходе обратно в full mode Topmost не сбрасывается.

**Текущий код:**
```csharp
if (_isCompactMode)
{
    Topmost = true;
}
```

**Исправление:**
```csharp
Topmost = _isCompactMode;
```

**Сложность:** 2 минуты

---

### CR-15: Слайдеры без live-preview [НИЗКАЯ / ОПЦИОНАЛЬНАЯ]

**Файл:** `AiteBar/AppSettingsWindow.xaml.cs:493-506`

**Проблема:** Слайдеры `SldPanelSize`, `SldZoneSize`, `SldDelay` показывают числа, но поведение панели не меняется до нажатия "Сохранить".

**Возможное исправление:** Добавить в обработчики value changed временное применение + откат при отмене:

```csharp
private double _originalPanelSize;
private double _originalZoneSize;
private double _originalDelay;

// При открытии окна:
_originalPanelSize = _settings.PanelSizePercent;
_originalZoneSize = _settings.ActivationZoneSizePercent;
_originalDelay = _settings.ActivationDelayMs;

private void SldPanelSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    if (_isLoadingSettings) return;
    if (TxtPanelSize != null) TxtPanelSize.Text = $"{(int)e.NewValue}%";
    _mainWindow.GetSettingsService().UpdateSettings(s => s.PanelSizePercent = e.NewValue);
    _mainWindow.RefreshPanel();
}

// При отмене:
private void BtnCancel_Click(...)
{
    _mainWindow.GetSettingsService().UpdateSettings(s =>
    {
        s.PanelSizePercent = _originalPanelSize;
        s.ActivationZoneSizePercent = _originalZoneSize;
        s.ActivationDelayMs = (int)_originalDelay;
    });
    _mainWindow.RefreshPanel();
    this.DialogResult = false;
    Close();
}
```

**Сложность:** 20 минут
**Приоритет:** Низкий — nice-to-have

---

### CR-16: Quick Note — рекурсивный semaphore [СРЕДНЯЯ]

**Файл:** `AiteBar/QuickNoteWindow.xaml.cs:184-194`

**Проблема:** `while (!await _saveSemaphore.WaitAsync(0))` — при быстрых сохранениях может привести к глубокой рекурсии (хотя на практике глубина ограничена do-while циклом внизу).

**Текущий код:**
```csharp
while (!await _saveSemaphore.WaitAsync(0))
{
    _saveAgainAfterCurrent = true;
    await _saveSemaphore.WaitAsync();   // blocks
    _saveSemaphore.Release();            // releases what we just acquired
    // ... check and possibly return or retry
}
```

**Исправление:** Заменить на простое блокирующее ожидание:
```csharp
await _saveSemaphore.WaitAsync();
```

Изменение minimal — убирает `while` цикл и делает поведение предсказуемым: текущий сохраняющий поток блокируется, пока предыдущий не завершится, затем проверяет `_hasPendingChanges`.

**Сложность:** 5 минут

---

### CR-18: Favicon скачивается без таймаута [СРЕДНЯЯ]

**Файл:** `AiteBar/MainWindow.DropHandler.cs:246-273`

**Проблема:** `IconHelper.DownloadFaviconAsync(val, currentDpi)` запускается без `CancellationToken`. Если сайт недоступен — задача может висеть бесконечно.

**Текущий код:**
```csharp
string? webIcon = await IconHelper.DownloadFaviconAsync(val, currentDpi);
```

**Исправление:**
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
string? webIcon = await IconHelper.DownloadFaviconAsync(val, currentDpi, cts.Token);
```

Если `DownloadFaviconAsync` не принимает `CancellationToken`, нужно добавить таймаут через `Task.WhenAny`:
```csharp
var downloadTask = IconHelper.DownloadFaviconAsync(val, currentDpi);
var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
var completed = await Task.WhenAny(downloadTask, timeoutTask);
string? webIcon = completed == downloadTask ? await downloadTask : null;
```

**Сложность:** 10 минут

---

### CR-21: File Sorter — устаревший FolderBrowserDialog [НИЗКАЯ]

**Файл:** `AiteBar/FileSorterWindow.xaml.cs:176-189`

**Проблема:** `Forms.FolderBrowserDialog` — WinXP-стиль диалог, визуально не вписывается в современный UI.

**Текущий код:**
```csharp
using var dialog = new Forms.FolderBrowserDialog
{
    Description = LocalizationService.Get("FileSorter_SelectFolderDialogTitle"),
    UseDescriptionForTitle = true,
    ShowNewFolderButton = false,
    SelectedPath = ...
};
```

**Исправление:** Заменить на `CommonOpenFileDialog` из WindowsAPICodePack:
```csharp
using var dialog = new CommonOpenFileDialog
{
    IsFolderPicker = true,
    Title = LocalizationService.Get("FileSorter_SelectFolderDialogTitle"),
    InitialDirectory = ...
};
if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
{
    _selectedCustomPath = dialog.FileName;
}
```

**Зависимость:** Требует NuGet-пакет `Microsoft-WindowsAPICodePack-Shell` или использование COM-интеропа.

**Сложность:** 10 минут + добавление зависимости
**Приоритет:** Низкий

---

## Сводка

| # | Что | Сложность | Приоритет |
|---|-----|-----------|-----------|
| CR-11 | Topmost compact mode | 2 мин | Средний |
| CR-16 | Semaphore рекурсия | 5 мин | Средний |
| CR-18 | Favicon таймаут | 10 мин | Средний |
| CR-21 | FolderBrowserDialog | 10 мин + NuGet | Низкий |
| CR-15 | Live-preview слайдеров | 20 мин | Низкий |

**Общая оценка:** ~45 минут

---

## Тестирование после исправлений

1. **CR-11:** TimerStopwatch → compact mode → full mode → окно НЕ поверх остальных
2. **CR-16:** Quick Note → быстро печатать 50+ символов → нет переполнения стека
3. **CR-18:** Drag URL недоступного сайта → элемент создаётся без иконки через 5 сек (не зависает)
