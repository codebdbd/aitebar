# AiteBar — План исправлений по результатам аудита

Обновлен 2026-06-15. Основан на проверке BEST_PRACTICES_AUDIT.md против текущего состояния кодовой базы.

Сборка Release: OK. Тесты: 484/484 OK. Регрессий нет.

---

## Этап 1 — Быстрые исправления (XS-S, низкий риск) ✓

### 1.1 BP-015: Снизить MaxSettingsFileBytes с 100MB до 10MB ✓

**Файл:** `AiteBar/AppSettingsService.cs:18`

**Что сделано:** Заменено `100 * 1024 * 1024` на `10 * 1024 * 1024`.

**Почему:** Нормальный settings файл — ~10-50KB. 100MB — чрезмерно и обесценивает guard.

**Тест:** Добавить/обновить unit test: файл > 10MB отклоняется.

---

### 1.2 BP-017: Добавить guard в BuildDuplicateElementName ✓

**Файл:** `AiteBar/MainWindow.xaml.cs:505`

**Что сделано:** Добавлено `index < 10000` в условие цикла и fallback с GUID.

**Почему:** Бесконечный цикл при дегенеративном случае.

**Тест:** Unit test с 1000 элементов с дублирующимися именами.

---

### 1.3 BP-020: Удалить мёртвый код _activeContextElements ✓

**Файл:** `AiteBar/MainWindow.xaml.cs:106`

**Что сделано:** Поле уже было удалено.

**Почему:** Мёртвый код увеличивает cognitive load.

**Тест:** Сборка без ошибок.

---

### 1.4 BP-013: Исправить Logger.RotateLogFile catch block ✓

**Файл:** `AiteBar/Logger.cs:73-77`

**Что сделано:** Уже исправлено — есть `catch (Exception ex)` и `Debug.WriteLine(ex)`.

**Почему:** Bare catch ловит `OutOfMemoryException` и др. Нет видимости ошибок ротации.

**Тест:** Manual: проверить что Debug output содержит сообщение при ошибке.

---

### 1.5 BP-042: Исправить mixed whitespace в AppSettingsService.cs ✓

**Файл:** `AiteBar/AppSettingsService.cs`

**Что сделано:** Запущен `dotnet format .\AiteBar.sln`.

**Почему:** ~250+ ошибок форматирования затрудняют code review и вызывают merge conflicts.

**Тест:** `dotnet format --verify-no-changes` возвращает 0 ошибок.

---

## Этап 2 — Частичные исправления (завершить начатое) ✓

### 2.1 BP-006: Убрать double DragHandle handling ✓

**Файл:** `AiteBar/MainWindow.xaml.cs:1992-2053`

**Что сделано:** Уже исправлено — `DragHandle_MouseLeftButtonUp` только освобождает захват мыши.

**Почему:** Два handler'а с одинаковой логикой — источник путаницы при рефакторинге.

**Тест:** Manual: drag-and-drop панели на все 4 стороны.

---

### 2.2 BP-009: Убрать дублирование в context switching ✓

**Файл:** `AiteBar/MainWindow.xaml.cs:299-372`

**Что сделано:** Уже исправлено — используется общий хелпер `GetNextContextId()`.

**Почему:** Изменение логики переключения требует правки в 2 местах.

**Тест:** Все 4 способа переключения контекста работают одинаково.

---

### 2.3 BP-010: Покрыть оставшиеся SaveAsync нотификацией ✓

**Файл:** `AiteBar/MainWindow.xaml.cs` — строки 1579, 1648, 1765

**Что сделано:** Уже исправлено — все вызовы используют `SaveSettingsWithNotificationAsync()`.

**Почему:** Пользователь не узнает о потере настроек при ошибке save.

**Тест:** Manual: reorder кнопок, unpin утилиты — при ошибке save видно dialog.

---

### 2.4 BP-018: Кэшировать brush конвертацию ✓

**Файл:** `AiteBar/MainWindow.xaml.cs`

**Что сделано:** Уже исправлено — есть `_brushCache` и `GetCachedBrush()`.

**Почему:** Каждый `RefreshPanel` создаёт N brush аллокаций (N = количество кнопок).

**Тест:** Performance test или manual: 50+ кнопок, проверка что GC pressure снижается.

---

## Этап 3 — Документация ✓

### 3.1 DOC-001: Исправить MaxUserBands в architecture.md ✓

**Файл:** `docs/architecture.md:537, 555`

**Что сделано:**
- Строка 537: заменено "максимум 2" на "максимум 3"
- Строка 555: заменено `MaxUserBands = 2` на `MaxUserBands = 3`

**Код:** `AiteBar/PanelLayoutHelper.cs:12` — `public const int MaxUserBands = 3;`

---

### 3.2 DOC-002/003/004: Исправить описание ActionService в architecture.md ✓

**Файл:** `docs/architecture.md:313-322`

**Что сделано:** Убрано упоминание полей, обновлено описание.

---

### 3.3 Добавить описание новых компонентов в architecture.md ✓

**Новые файлы:**
- `AiteBar/OverflowWrapPanel.cs` — кастомный WPF Panel для multi-band layout
- `AiteBar/UnifiedButton.cs` — модель unified button
- `AiteBar/UpdateCheckUi.cs` — UI-хелпер для update check
- `AiteBar/UpdateCheckService.cs` — сервис проверки обновлений

**Что сделано:** Добавлены упоминания в соответствующие слои architecture.md.

---

## Приоритетный backlog ✓

Все задачи выполнены!

---

## Что НЕ нужно исправлять

- BP-001 (Handled = true) — исправлено
- BP-002 (data race lock) — исправлено
- BP-004 (finalizer) — исправлено
- BP-005 (GitHub Actions) — исправлено
- BP-007 (ISettingsWindowContext) — исправлено
- BP-008 (Thread.Sleep → Task.Delay) — исправлено
- BP-012 (HTTP downgrade) — исправлено
- BP-024 (ZIP Slip) — исправлено

