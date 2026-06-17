# Инструкция по реализации плана «Безопасное разделение MainWindow»

> 📋 **Основной план**: [MAINWINDOW_SPLIT_PLAN.md](./MAINWINDOW_SPLIT_PLAN.md) — исходный план с анализом и сроками.

## Предварительные шаги
1. ✅ Убедиться, что все изменения сохранены в git
2. ✅ Запустить сборку в Release: `dotnet build .\AiteBar.sln -c Release`
3. ✅ Запустить все тесты: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`
4. ✅ Убедиться, что нет ошибок и предупреждений

---

## Общий принцип
- Использовать **partial class** для MainWindow
- Не менять **ничего** в логике — только физическое разделение
- Каждый шаг проверять сборкой и тестами
- После каждого шага делать git commit

---

## Шаг 1: TrayMenuHandler (≈50 строк) — НИЗКИЙ РИСК
**Цель**: Вынести логику работы с треем в отдельный файл

1. Создать файл `AiteBar/MainWindow.TrayMenuHandler.cs`
2. Добавить в него:
   ```csharp
   namespace AiteBar;

   public partial class MainWindow
   {
       // сюда переносим методы:
       // - InitTrayIcon()
       // - ShowTrayContextMenu()
   }
   ```
3. Перенести методы `InitTrayIcon()` и `ShowTrayContextMenu()` из `MainWindow.xaml.cs` в новый файл
4. Убедиться, что все using-пространства имён присутствуют
5. Проверить сборку: `dotnet build .\AiteBar.sln -c Release`
6. Проверить тесты: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`
7. Проверить вручную: запустить приложение, проверить трей-меню
8. Сделать git commit: `git add . && git commit -m "Step 1: Extract TrayMenuHandler"`

---

## Шаг 2: KeyboardNavigationHandler (≈150 строк) — НИЗКИЙ РИСК
**Цель**: Вынести логику клавиатурной навигации

1. Создать файл `AiteBar/MainWindow.KeyboardNavigationHandler.cs`
2. Добавить в него:
   ```csharp
   namespace AiteBar;

   public partial class MainWindow
   {
       // сюда переносим:
       // - enum PanelInputMode
       // - SetPanelInputMode()
       // - UpdateAllButtonsFocusVisualStyle()
       // - Window_PreviewKeyDown() (если есть)
       // - FocusPanelForKeyboard() (если есть)
       // - IsPanelKeyboardMode свойство
       // - OnDeactivated()
       // - OnActivated()
   }
   ```
3. Перенести соответствующие методы и enum
4. Проверить сборку и тесты
5. Проверить вручную: переключение режима клавиатуры/указателя
6. Git commit: `git add . && git commit -m "Step 2: Extract KeyboardNavigationHandler"`

---

## Шаг 3: PanelDragHandler (≈100 строк) — НИЗКИЙ РИСК
**Цель**: Вынести логику перетаскивания панели для смены края

1. Создать файл `AiteBar/MainWindow.PanelDragHandler.cs`
2. Добавить в него:
   ```csharp
   namespace AiteBar;

   public partial class MainWindow
   {
       // сюда переносим:
       // - DragHandle_MouseDown
       // - DragHandle_MouseMove
       // - DragHandle_MouseUp
       // - EndPanelDragAsync
       // - SetDragHandleActive
       // - SetPanelDragRenderingActive
       // и связанные поля (_isPanelDragging, _panelDragChanged и т.д.)
   }
   ```
3. Перенести методы и поля
4. Проверить сборку и тесты
5. Проверить вручную: перетащить панель на все 4 края
6. Git commit: `git add . && git commit -m "Step 3: Extract PanelDragHandler"`

---

## Шаг 4: DropHandler (≈150 строк) — НИЗКИЙ РИСК
**Цель**: Вынести логику перетаскивания файлов на панель

1. Создать файл `AiteBar/MainWindow.DropHandler.cs`
2. Добавить в него:
   ```csharp
   namespace AiteBar;

   public partial class MainWindow
   {
       // сюда переносим:
       // - Border_DragOver
       // - Border_Drop
       // - TryGetDropTarget
       // - CanAcceptDropData
   }
   ```
3. Перенести методы
4. Проверить сборку и тесты
5. Проверить вручную: перетащить файлы на панель
6. Git commit: `git add . && git commit -m "Step 4: Extract DropHandler"`

---

## Шаг 5: ImportExportHandler (≈100 строк) — НИЗКИЙ РИСК
**Цель**: Вынести логику импорта/экспорта панелей

1. Создать файл `AiteBar/MainWindow.ImportExportHandler.cs`
2. Добавить в него:
   ```csharp
   namespace AiteBar;

   public partial class MainWindow
   {
       // сюда переносим:
       // - ExportCurrentPanelAsync
       // - ImportIntoCurrentPanelAsync
       // - BuildPanelPackageFileName
   }
   ```
3. Перенести методы
4. Проверить сборку и тесты
5. Проверить вручную: экспортировать и импортировать панель
6. Git commit: `git add . && git commit -m "Step 5: Extract ImportExportHandler"`

---

## Шаг 6: DragAndDropHandler (≈250 строк) — НИЗКИЙ РИСК
**Цель**: Вынести логику перетаскивания кнопок внутри панели

1. Создать файл `AiteBar/MainWindow.DragAndDropHandler.cs`
2. Добавить в него:
   ```csharp
   namespace AiteBar;

   public partial class MainWindow
   {
       // сюда переносим:
       // - CreateUnifiedButton() (часть с drag-handlers)
       // - CalculateUnifiedTargetIndex
       // - UpdateUnifiedReorderPositions
       // - ReorderUserElementsAsync
       // - ReorderUtilityButtonsAsync
       // и связанные поля (_draggedButton, _isReordering, _draggedOriginalIndex и т.д.)
   }
   ```
3. Перенести методы и поля
4. Проверить сборку и тесты
5. Проверить вручную: перетащить кнопки, изменить их порядок
6. Git commit: `git add . && git commit -m "Step 6: Extract DragAndDropHandler"`

---

## Финальная проверка
1. Сборка: `dotnet build .\AiteBar.sln -c Release` — нет ошибок
2. Тесты: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` — все 488 прошли
3. Размер MainWindow.xaml.cs уменьшился до ≈1500 строк
4. Проверить **все 4 стороны панели** (Top/Bottom/Left/Right):
   - Показ/скрытие
   - Перетаскивание панели
   - Перетаскивание кнопок
   - Клавиатурная навигация
   - Трей-меню
   - Импорт/экспорт
5. Сделать финальный git commit (если нужно)

---

## Чеклист для каждого шага
- [ ] Создан новый partial-файл
- [ ] Методы и поля перенесены без изменений
- [ ] Все using-пространства имён присутствуют
- [ ] Сборка прошла без ошибок
- [ ] Все тесты прошли
- [ ] Вручную проверена работа функции
- [ ] Сделан git commit

---

## Важные правила
1. **НЕ МЕНЯЙТЕ ЛОГИКУ!** Только физическое разделение
2. **Каждый шаг — отдельный git commit**
3. **Проверяйте все 4 стороны панели** после каждого шага
4. **Не перескакивайте через шаги** — идите последовательно
5. **Если что-то пошло не так** — откатите последний commit

---

## Что должно получиться в итоге
```
AiteBar/
├── MainWindow.xaml
├── MainWindow.xaml.cs          (≈1500 строк, основная логика)
├── MainWindow.TrayMenuHandler.cs
├── MainWindow.KeyboardNavigationHandler.cs
├── MainWindow.PanelDragHandler.cs
├── MainWindow.DropHandler.cs
├── MainWindow.ImportExportHandler.cs
└── MainWindow.DragAndDropHandler.cs
```

---

## Если возникли проблемы
1. Проверьте, что все using есть в partial-файлах
2. Проверьте, что все поля и методы доступны (не забыли `private`, `protected` и т.д.)
3. Закройте Visual Studio/Trae, если WPF temp-файлы залочены
4. Откатите последний commit: `git reset HEAD~1 --hard`
