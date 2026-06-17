# План исправлений по Code Review

## Статус: 2 из 12 проблем исправлено (не включая MainWindow)

---

## ✅ Уже исправленные проблемы

| Проблема | Файл | Статус |
|----------|------|--------|
| Двойной вызов SettingsChanged | AppSettingsService.cs | ✅ Исправлено |
| Logger блокирует ввод-вывод | Logger.cs | ✅ Исправлено |
| Магические числа в MainWindow/PanelLayoutHelper | Constants.cs | ✅ Исправлено |
| Инъекция в Process.Start | MainWindow.xaml.cs | ✅ Исправлено |
| Ручная дубликация в CloneAppSettings | AppSettingsService.cs | ✅ Исправлено |

---

## Оставшиеся проблемы (требуют изменений в MainWindow)

По вашему запросу **MainWindow не трогался**, поэтому следующие шаги не выполнены:

---

## Шаг 1: Удалить неиспользуемое поле `_mouseWheelCaptureToken`

**Файл:** `MainWindow.xaml.cs:1705`

**Проблема:** Поле `_mouseWheelCaptureToken` объявлено, но нигде не читается и не используется.

**Исправление:** Удалить строку `private int _mouseWheelCaptureToken = 0;`

---

## Шаг 2: Дедупликация switch-case для SettingsKey

**Файлы:**
- `MainWindow.xaml.cs:1378-1391` (BuildUnifiedButtonContextMenu — выключение утилиты)
- `UnifiedButtonService.cs:98-116` (GetUtilityVisibility)

**Проблема:** Маппинг строковых ключей (`"ShowPresetSearch"` → свойство настроек) дублируется в двух местах. При добавлении новой утилиты легко забыть обновить одно из мест.

**Исправление:**
1. Добавить в `AppSettingsService` метод `SetUtilityVisibility(string key, bool visible)`, который делает switch-case один раз
2. В `MainWindow.xaml.cs` вызвать `_settingsService.SetUtilityVisibility(item.SettingsKey, false)` вместо дублирующего switch-case
3. В `UnifiedButtonService.GetUtilityVisibility` вызвать `_settingsService.GetUtilityVisibility(key)` вместо собственного switch-case

---

## Шаг 3: Добавить try/catch в оставшиеся async void

**Файл:** `MainWindow.xaml.cs`

**Проблема:** Некоторые `async void` обработчики не защищены от необработанных исключений.

**Методы, требующие обёртки:**
- `ActivateContextRelative` (строка 339)
- `ActivateContextByIndex` (строка 354)
- `ActivateContextById` (строка 690)
- `BtnAppSettings_Click` (строка 1747)

**Исправление:** Обернуть тела этих методов в try/catch с `Logger.Log(ex)`.

Обратите внимание: `BtnSearch_Click`, `BtnScreenshotRegion_Click` и другие `Btn*_Click` уже вызывают `RunPresetActionAsync`, который имеет try/catch — они в порядке.

---

## Шаг 4: Оптимизация `_buttonImageCache.Clear()`

**Файл:** `MainWindow.xaml.cs:1256`

**Проблема:** `_buttonImageCache.Clear()` вызывается при каждом `RefreshPanel()`, даже если изображения не изменились. Это приводит к повторной загрузке всех кастомных иконок.

**Исправление:**
1. Не очищать кэш полностью — вместо этого удалять только записи, которых нет в текущем наборе кнопок
2. Или: очищать кэш только если `Elements` действительно изменились (сравнивать хэш/версию)

Простой вариант: проверять, изменился ли список элементов, и очищать кэш только в этом случае.

---

## Шаг 5: Кэшировать `ToList()` в `GetNextContextId`

**Файл:** `MainWindow.xaml.cs:283-299`

**Проблема:** `ContextStateHelper.GetEnabledContexts(AppSettings.Contexts)` возвращает `IReadOnlyList`, но дальше вызывается `enabledContexts.ToList().FindIndex(...)` — создаёт лишнюю аллокацию.

**Исправление:** `GetEnabledContexts` уже возвращает `List<PanelContext>` (через `.ToList()` внутри хелпера). Убрать лишний `.ToList()` в `MainWindow` — работать с `IReadOnlyList` напрямую через индексацию или `FindIndex` (он доступен на `IReadOnlyList` через LINQ).

Та же проблема в:
- `TryActivateContext` (строка 259) — **уже исправлено** (вызывается `ToList()` один раз)
- `GetNextContextId` (строка 285) — нужна проверка

---

## Шаг 6: Безопасность — валидация путей в `OpenElementLocationAsync`

**Файл:** `MainWindow.xaml.cs:613-652`

**Проблема:** Ранее была уязвимость инъекции, но **уже исправлено** — теперь используется `ProcessStartInfo.ArgumentList`.

---

## Приоритеты

| Шаг | Сложность | Время | Риск |
|-----|-----------|-------|------|
| 1 | Trivial | 1 мин | Нет |
| 2 | Medium | 15 мин | Низкий |
| 3 | Easy | 10 мин | Низкий |
| 4 | Medium | 10 мин | Низкий |
| 5 | Trivial | 2 мин | Нет |
| 6 | Easy | 5 мин | Нет |

**Общее время:** ~40 минут
