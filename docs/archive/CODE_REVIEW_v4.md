# AiteBar — Код-ревью v4

Дата: 2026-06-21
Сборка: ✅ Release | Тесты: 519/519 ✅

---

## CRITICAL

### 1. `AppSettingsService.cs:60` — `UpdateSettings` отбрасывает изменения элементов

```csharp
var next = CloneAppSettings(_appSettings);   // 57: clone
next.Elements = [.. _elements];              // 58: подставить текущие elements
update(next);                                 // 59: delegate мутирует next
next.Elements = [.. _elements];              // 60: ПЕРЕЗАПИСАТЬ оригинальными — delegate потерял!
```

Строка 60 отменяет любые изменения `Elements`, которые delegate мог сделать на строке 59. Сейчас delegate'ы не трогают elements — баг latent, но ловушка для будущего кода.

### 2. `Logger.cs:149` — Fallback ротации логов уничтожает всю историю

```csharp
catch (Exception ex)
{
    Debug.WriteLine(ex);
    File.WriteAllText(LogPath, string.Empty);  // ← ВСЯ ИСТОРИЯ УДАЛЕНА
}
```

Если `File.Move` падает (антивирус, блокировка, права), catch обрезает лог-файл до пустоты. Часы/дни диагностики пропадают безвозвратно.

---

## HIGH

### 3. `LocalizationService.cs:192-195` — Смена культуры влияет на все потоки

```csharp
CultureInfo.CurrentCulture = culture;
CultureInfo.CurrentUICulture = culture;
CultureInfo.DefaultThreadCurrentCulture = culture;      // ← глобально
CultureInfo.DefaultThreadCurrentUICulture = culture;    // ← глобально
```

`DefaultThreadCurrentCulture` задаёт культуру для ВСЕХ будущих потоков. Фоновые задачи (скачивание favicon, сериализация JSON) могут неверно распарсить числа/даты при смене языка.

### 4. `TelemetryService.cs:35` — `_initialized = true` до фактической инициализации

```csharp
lock (SyncRoot)
{
    if (_initialized) return;
    _initialized = true;    // 35: УСТАНОВЛЕН
}
// ... 38-95: фактическая работа (может упасть/early-return)
```

Если Sentry SDK не инициализируется (нет DSN, ошибка), повторный вызов `InitializeAsync` сразу вернётся — telemetry отключена на всю сессию без retry.

### 5. `ActionService.cs:246` — Мутация `CustomElement` in-place при ротации профилей

```csharp
string prof = el.UseRotation ? AdvanceRotationProfile(el) : el.ChromeProfile;
el.LastUsedProfile = prof;      // 246: мутирует оригинальный объект
await _settingsService.SaveAsync();
```

`el` — ссылка из `_elements`. При двух быстрых вызовах `AdvanceRotationProfile` читает `el.LastUsedProfile` до того как предыдущий save завершился — ротация может сбиться.

### 6. `MainWindow.xaml.cs:122-126` — `OnSettingsChanged` игнорирует ошибки регистрации hotkeys

```csharp
private void OnSettingsChanged(object? sender, EventArgs e)
{
    UnregisterGlobalHotkey();
    RegisterGlobalHotkey();     // возвращает List<string> с ошибками — ИГНОРИРУЕТСЯ
}
```

`RegisterGlobalHotkey()` возвращает failed display names, но результат отбрасывается. Пользователь не узнает что клавиша не зарегистрировалась.

---

## MEDIUM

### 7. `AppSettingsService.cs:798` — `SaveElementAsync` хранит element по ссылке

```csharp
_elements[_elements.IndexOf(existing)] = updated;  // ссылка, не clone
```

Все остальные методы хранят clone. Если caller изменит `updated` после вызова (до завершения `SaveAsync`), в JSON попадёт inconsistent состояние.

### 8. `HotkeyService.cs:130-155` — Частичная регистрация без отката

`UnregisterAll` сначала удаляет все горячие клавиши, затем регистрирует по одной. Если 3-я из 5 падает — 1,2,4,5 остаются. Состояние: часть команд работает, часть нет.

### 9. `ActionService.cs:511-513` — F11 отправляется в неверное окно

```csharp
_runtime.SetForegroundWindow(proc.MainWindowHandle);  // 511: ненадёжный Win32
await _runtime.DelayAsync(FullscreenForegroundDelayMs); // 100ms — хак
SendVirtualKey((byte)KeyInterop.VirtualKeyFromKey(Key.F11)); // 513: F11 кому?
```

`SetForegroundWindow` ненадёжный на Windows (OS ограничивает). Если за 100ms другое окно перехватит фокус — F11 уйдёт туда.

### 10. `MainWindow.xaml.cs:1523` — `SourceElement!` без null-проверки

```csharp
return BuildElementContextMenu(item.SourceElement!);
```

Если `SourceElement == null` (повреждённые настройки) — `NullReferenceException`.

### 11. `ClipboardHistoryService.cs:197-198` — `SequenceEqual` на полных image byte arrays

```csharp
entry.ImageBytes.SequenceEqual(imageBytes)  // до 5 МБ × 50 записей
```

При каждом изменении clipboard — сравнение до 250 МБ данных. Стоит сравнивать длину или хеш первых байт.

### 12. `MainWindow.xaml.cs:8` — `_timer` 30ms polling (~33fps)

```csharp
private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
```

Постоянное потребление CPU для проверки позиции курсора. 100-200ms было бы достаточно.

### 13. `QuickNoteWindow.xaml.cs:116-119` — Таймеры не останавливаются в `OnClosed`

```csharp
protected override void OnClosed(EventArgs e)
{
    base.OnClosed(e);  // таймеры НЕ остановлены
}
```

`_saveTimer` и `_geometrySaveTimer` продолжают работать после закрытия окна до следующего тика.

### 14. `MainWindow.xaml.cs:662,673` — `Process.Start` без Dispose

```csharp
Process.Start(psi);  // Process хендл не освобождается
```

Нарушение паттерна IDisposable. На Windows хендлы освобождаются при выходе, но это не best practice.

### 15. `PanelLayoutHelper.cs:256-266` — 3-диапазонный cap обрезает кнопки без индикатора

Если кнопок больше чем 3 полосы — лишние не рендерятся. OverflowWrapPanel не показывает индикатор "ещё N кнопок".

### 16. `ClipboardManagerWindow.xaml.cs:87` — Полная пересборка UI при каждом keystroke

`EntriesPanel.Children.Clear()` + пересоздание всех WPF-элементов каждые 100ms при вводе в поиск.

### 17. `MainWindow.xaml.cs:1345-1410` — `RefreshPanel` пересоздаёт весь UI

При каждом переключении контекста — `Children.Clear()` + пересоздание всех кнопок. Видимый flicker.

### 18. `ScreenColorPickerWindow.cs:43-51` — Full screen capture на UI-потоке в конструкторе

`graphics.CopyFromScreen(...)` блокирует UI при открытии. На 3×4K: ~99 МБ bitmap.

### 19. `Logger.cs:139` — Ротация бэкапов по `CreationTimeUtc`

`File.GetCreationTimeUtc` не меняется при move/copy. Файл из другого места сохраняет старое время — ротация удалит неправильные бэкапы.

### 20. `UnifiedButtonService.cs:51` — Deep clone AppSettings только для `UtilityButtonOrder`

`_settingsService.Settings` клонирует весь объект (элементы, контексты, настройки) ради чтения одного списка строк.

### 21. `TelemetryService.cs:98-124` — Чтение settings файла параллельно с записью

`TelemetryService` independently читает `settings.json` пока `AppSettingsService` его пишет. Частично записанный JSON может десериализоваться как null.

### 22. `AppSettingsService.cs:385-413` — `File.ReadAllText` синхронный в async-пути

`TryLoadFromBackup` блокирует поток при чтении на медленном диске.

---

## LOW

| # | Файл:Строка | Проблема |
|---|-------------|----------|
| 23 | `ActionService.cs:159-177` | Мёртвый код: проверка browser-not-found недостижима в hotkey-handler (actionType == Hotkey, не Web) |
| 24 | `MainWindow.xaml.cs:337-350 vs 352-372` | Дублирование `SwitchActiveContextAsync` и `ActivateContextRelative` |
| 25 | `ActivationZoneHelper.cs:29-30` | Off-by-one: последний пиксель не в зоне активации |
| 26 | `PanelPositionHelper.cs:46` | LINQ `OrderBy().First()` для 4 элементов |
| 27 | `PathHelper.cs:49-52` | Redundant `Directory.Exists` перед `CreateDirectory` |
| 28 | `OverflowWrapPanel.cs:114-126` | Новый `List<UIElement>` в каждом Measure/Arrange |
| 29 | `AppSettingsWindow.xaml.cs:488-492` | `BrushConverter` на каждый вызов `GetPanelBadgeBrush` |
| 30 | `NativeIntegrationService.cs:10-72` | Finalizer для Win32 hook — ненадёжен на другом потоке |
| 31 | `AppSettingsService.cs:439-476` | Temp file leak при crash процесса |
| 32 | `Models.cs:141-145` | Неконсистентные отступы |

---

## Сводка

| Категория | Кол-во |
|-----------|--------|
| Critical | 2 |
| High | 4 |
| Medium | 16 |
| Low | 10 |
| **Итого** | **32** |

---

## Топ-5 приоритетов

1. **Logger.cs:149** — Убрать `File.WriteAllText(LogPath, string.Empty)`. Вместо этого: оставить файл как есть, залогировать ошибку.
2. **AppSettingsService.cs:60** — Убрать `next.Elements = [.. _elements]` или документировать что elements не обновляются через delegate.
3. **TelemetryService.cs:35** — Перенести `_initialized = true` после фактической инициализации Sentry.
4. **LocalizationService.cs:194-195** — Убрать `DefaultThreadCurrentCulture` / `DefaultThreadCurrentUICulture`.
5. **ActionService.cs:246** — Клонировать `CustomElement` перед мутацией `LastUsedProfile`.
