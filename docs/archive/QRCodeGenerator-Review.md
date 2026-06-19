# QRCodeGenerator — Аудит и рекомендации

## Статус аудита

Дата: 2026-06-19
Версия кода: актуальная
Тесты: 14/14 passed

---

## 1. Соответствие паттерну утилит

### Архитектура: СООТВЕТСТВУЕТ

| Критерий | Ожидание | Факт | Статус |
|----------|----------|------|--------|
| Utility class | `UtilityBase<TWindow>` + `[Utility]` + `[SupportedOSPlatform]` | `QRCodeGeneratorUtility` — наследует `UtilityBase<QRCodeGeneratorWindow>`, оба атрибута присутствуют | OK |
| Window class | `DarkWindow` + `ShowNearPanel()` | `QRCodeGeneratorWindow : DarkWindow`, `ShowNearPanel` скопирован из `IconConverterWindow` | OK |
| Service class | Отдельный сервис с логикой | `QRCodeService` — чистая логика, нет UI-зависимостей | OK |
| Models class | Отдельный файл моделей | `QRCodeModels.cs` — `QRCodeEccLevel`, `QRCodeGenerationOptions`, `QRCodeGenerationResult` | OK |
| Регистрация в панели | `UnifiedButtonService.UtilityButtons` | Строка добавлена | OK |
| Регистрация в MainWindow | `ExecuteUnifiedButtonActionAsync()` switch | Case добавлен | OK |
| Visibility toggle | `AppSettings.ShowPreset*` | `ShowPresetQRCodeGenerator = false` | OK |
| Hotkey | `HotkeyCommand` enum + `HotkeyBinding` | Добавлены enum, ID, descriptor, binding, dispatch | OK |
| Localization | `Tool_*` + `Main_*Tooltip` keys | Добавлены в все 4 .resx файла | OK |
| SettingsWindow | Checkbox + hotkey controls | Добавлены в XAML и code-behind | OK |
| Get/SetUtilityVisibility | Switch/case в `AppSettingsService` | Добавлены оба case | OK |

### Архитектура: РАСХОЖДЕНИЯ

**1. Конструктор Window — без параметров vs с AppSettingsService**

| Утилита | Конструктор Window |
|---------|-------------------|
| IconConverterWindow | `(AppSettingsService settingsService)` |
| TimerStopwatchWindow | `()` |
| FileSorterWindow | `(AppSettingsService settingsService)` |
| QuickNoteWindow | `(QuickNoteService, AppSettingsService)` |
| **QRCodeGeneratorWindow** | **`()`** |

`QRCodeGeneratorWindow` не принимает `AppSettingsService` в конструкторе. Это допустимо (как `TimerStopwatchWindow`), потому что настройки не нужны для начальной работы — QR-код генерируется из текста пользователя. Но это значит, что если в будущем понадобятся настройки (например, сохранение последнего ECC level), придётся менять сигнатуру.

**Рекомендация**: Оставить как есть — конструктор без параметров упрощает создание. Если понадобятся настройки — добавить позже.

**2. Отсутствие `_settingsService` в Window**

| Утилита | Сохраняет `_settingsService`? |
|---------|------------------------------|
| IconConverterWindow | Нет (только в `ShowNearPanel`) |
| TimerStopwatchWindow | Да (`_settingsService` field, сохраняет в `Closed`) |
| FileSorterWindow | Да (`_settingsService` field) |
| QuickNoteWindow | Да (`_settingsService` field, auto-save) |
| **QRCodeGeneratorWindow** | **Нет** |

Это нормально для утилиты, которая не сохраняет состояние между сессиями. QR-код — это ephemeral: ввёл текст, получил картинку. Нет настроек, которые нужно сохранять.

---

## 2. Соответствие дизайну

### Визуальные элементы: СООТВЕТСТВУЕТ

| Элемент | Ожидание (IconConverter/TimerStopwatch) | Факт (QRCodeGenerator) | Статус |
|---------|----------------------------------------|------------------------|--------|
| Window chrome | `WindowStyle=None`, `AllowsTransparency=True`, `Topmost=True`, `ShowInTaskbar=False` | Идентично | OK |
| Outer border | `Margin="8"`, `CornerRadius="8"`, `Background={StaticResource BorderColor}`, `BorderBrush="#332A9CFF"`, `BorderThickness="0.7"` | Идентично | OK |
| Shadow | `DropShadowEffect BlurRadius=14 ShadowDepth=2 Direction=270 Opacity=0.45` | Идентично | OK |
| Inner border | `CornerRadius="7"`, `Background={StaticResource PanelBackground}` | Идентично | OK |
| Content margin | `Margin="18"` | Идентично | OK |
| Header row | `Height="32"`, title `FontSize=14 FontWeight=SemiBold Foreground=White`, close `&#xF369;` | Идентично | OK |
| Card background | `#151922` + `BorderBrush="#273344"` + `CornerRadius="6"` | Идентично | OK |
| Preview background | `#101318` + `BorderBrush="#273344"` + `CornerRadius="6"` | Идентично | OK |
| Button styles | `CommandButtonStyle` + `PrimaryCommandButtonStyle` | Идентично | OK |
| Status text | `{StaticResource MutedText} FontSize="11"` | Идентично | OK |
| Styles | `HeaderButtonStyle`, `CommandButtonStyle`, `PrimaryCommandButtonStyle`, `FieldLabelStyle` | Скопированы из `IconConverterWindow.xaml` | OK |

### Визуальные элементы: РАСХОЖДЕНИЯ

**1. `CommandButtonStyle` — Height и MinWidth**

| Свойство | IconConverterWindow | QRCodeGeneratorWindow |
|----------|--------------------|-----------------------|
| `Height` | `34` | `36` |
| `MinWidth` | `72` | `112` |
| `Padding` | `14,0` | `12,0` |

QRCodeGenerator использует `Height=36` вместо `34` и `MinWidth=112` вместо `72`. Это незначительные отклонения. `MinWidth=112` делает кнопки шире — для 3 кнопок в `UniformGrid Columns="3"` это выглядит хорошо, но не совпадает с другими утилитами.

**Рекомендация**: Привести к `Height="34"` и `MinWidth="72"` для консистентности, либо зафиксировать отклонение как осознанное решение для 3-колоночного layout.

**2. `FieldLabelStyle` — добавлен `TextTrimming`**

| Свойство | IconConverterWindow | QRCodeGeneratorWindow |
|----------|--------------------|-----------------------|
| `TextTrimming` | Отсутствует | `CharacterEllipsis` |

QRCodeGenerator добавляет `TextTrimming="CharacterEllipsis"` в `FieldLabelStyle`. Это хорошее улучшение — предотвращает обрезку текста меток. Но отличается от оригинала.

**Рекомендация**: Добавить `TextTrimming="CharacterEllipsis"` во все утилиты (как улучшение), либо убрать из QRCodeGenerator для консистентности.

**3. Window size — больше чем в плане**

| Параметр | План | Факт |
|----------|------|------|
| `Width` | `480` | `540` |
| `Height` | `440` | `520` |
| `MinWidth` | `420` | `500` |
| `MinHeight` | `380` | `480` |

Увеличение связано с `AcceptsReturn="True"` (многострочный ввод). При высоте `520` input-область получает достаточно места. Это осознанное изменение.

**4. Row heights — фиксированные вместо Auto**

| Row | QRCodeGeneratorWindow | Ожидание |
|-----|----------------------|----------|
| Row 1 (input) | `Height="108"` | `Height="Auto"` |
| Row 4 (status+buttons) | `Height="76"` | `Height="Auto"` |

`IconConverterWindow` использует фиксированные高度ы для preview ячеек (`90`, `112`), но input-область и bottom-row — `Auto`. QRCodeGenerator зафиксировал input-область на `108` и bottom-row на `76`. Это допустимо, но при `ResizeMode="CanResizeWithGrip"` фиксированные высоты могут выглядеть не идеально при ресайзе.

**Рекомендация**: Заменить `Height="108"` и `Height="76"` на `Height="Auto"` для лучшего ресайза.

---

## 3. Код-ревью по файлам

### QRCodeService.cs

**Позитивы:**
- Чистое разделение `GenerateAsync` / `GenerateQrData` / `RenderPng` / `RenderSvg`
- `NormalizeOptions` + `ValidateRenderOptions` — хорошая нормализация
- `NormalizeColor` — ручная валидация hex без зависимости от SkiaSharp
- `MapEccLevel` — чистый switch expression

**Замечания:**

1. **Дублирование валидации** (`GenerateQrData:47-55` и `NormalizeOptions:96-104`): Оба метода проверяют `IsNullOrWhiteSpace` и `Length > 4296`. `NormalizeOptions` вызывается из `GenerateAsync`, а `GenerateQrData` — отдельно. Если вызывать `GenerateQrData` напрямую (как в `RefreshPreviewAsync`), валидация идёт только через `GenerateQrData`. Если через `GenerateAsync` — дважды.

2. **`ParseColorBytes`** (`:142-151`): Полагается на `NormalizeColor`, guarantee 7-символьный hex. Безопасно при текущей реализации, но хрупко при изменениях.

3. **`GetVersion` формула** (`:90`): `moduleCount <= 21 ? 1 : ((moduleCount - 21) / 4) + 1`. Это корректная формула для стандартного QR (version N имеет 4*N+17 модулей). Проверено: version 1 = 21, version 2 = 25, ..., version 40 = 177.

### QRCodeGeneratorWindow.xaml.cs

**Позитивы:**
- Debounce через `Task.Delay(180)` + `CancellationToken` — правильный паттерн
- `EnsureRenderedArtifactsAsync()` — ленивая генерация PNG/SVG только при copy/save
- `CreateBitmapImage` с `Freeze()` — потокобезопасно
- `OnClosed` — очистка CTS
- `OnLocalizationChanged` — обновление при смене языка
- Placeholder через `TextBlock` overlay — правильный WPF паттерн

**Замечания:**

1. **CancellationToken disposal** (`:76-78`):
```csharp
_previewRequestCts?.Cancel();
_previewRequestCts?.Dispose();
_previewRequestCts = new CancellationTokenSource();
```
`Dispose()` после `Cancel()` — правильный порядок. Но `RefreshPreviewAsync` уже захватил `token` до `Dispose()`. Это безопасно, потому что `CancellationToken` — это value type (struct), и `Dispose()` не делает ничего с уже захваченным токеном. Но для ясности можно убрать `Dispose()` — GC справится.

2. **UI update без Dispatcher** (`:101-102`): `ImgPreview.Source = ...` вызывается из continuation `Task.Delay(180)`, который синхронизируется через `SynchronizationContext`. Безопасно, но неявно. Явный `Dispatcher.InvokeAsync` был бы надёжнее (как в других утилитах).

3. **Кэширование**: `_lastPngBytes` и `_lastSvgContent` сбрасываются при каждом keystroke. При copy/save вызывается полная перегенерация через `_service.GenerateAsync()`. Это избыточно — `QRCodeData` уже доступен из `RefreshPreviewAsync`.

### QRCodeModels.cs

Чисто. Модели immutable (`init` properties). Единственное: нет XML doc comments, но это consistent с другими моделями проекта.

### QRCodeGeneratorUtility.cs

Чисто. Следует паттерну `IconConverterUtility`. `IconGlyph = "\uF635"` — MaterialIcons QR code glyph.

### Тесты (QRCodeServiceTests.cs)

- 14 тестов, все проходят
- Покрывают: happy path, PNG header, SVG tag, empty/whitespace/long text, ECC levels, все render методы
- `Theory` с `[InlineData]` для ECC levels — хорошая практика
- **Нет тестов для**: `NormalizeColor`, `ParseColorBytes`, `ValidateRenderOptions` (private methods, но можно测试 через `RenderPng`/`RenderSvg` с некорректными данными)

---

## 4. Итоговая матрица

| Категория | Оценка | Детали |
|-----------|--------|--------|
| Архитектура | Отлично | Чистое разделение models/service/window/utility |
| Паттерны утилит | Хорошо | 3 незначительных отклонения (конструктор, styles, row heights) |
| UI/Дизайн | Хорошо | Цвета/layout совпадают, 2 style-отклонения |
| Безопасность | Отлично | Нет secrecy issues, нет dangerous patterns |
| Производительство | Хорошо | Debounce, lazy generation, но кэширование можно улучшить |
| Тесты | Хорошо | 14 тестов, покрытие core logic |
| Локализация | Отлично | Все строки через `LocalizationService` |
| Ошибки | Хорошо | `Logger.Log` + user-friendly сообщения |

---

## 5. Рекомендации

### Приоритет 1 — Исправить

1. **Привести `CommandButtonStyle` к стандарту**: `Height="34"`, `MinWidth="72"`, `Padding="14,0"` (как в `IconConverterWindow.xaml:45-47`)

2. **Заменить фиксированные row heights на Auto**: `Row 1 Height="108"` → `Auto`, `Row 4 Height="76"` → `Auto` для корректного ресайза

### Приоритет 2 — Рекомендуется

3. **Убрать дублирование валидации**: `NormalizeOptions` проверяет текст, `GenerateQrData` проверяет повторно. Оставить валидацию только в одном месте.

4. **Кэшировать `QRCodeData`**: Вместо `_lastPngBytes = null` при каждом keystroke, сохранять `QRCodeData` и переиспользовать для `EnsureRenderedArtifactsAsync`. Это уберёт повторную генерацию при copy/save.

5. **Явный Dispatcher для UI update**: Добавить `Dispatcher.InvokeAsync()` для `ImgPreview.Source = ...` в `RefreshPreviewAsync` для явной синхронизации.

### Приоритет 3 — Желательно

6. **Добавить `TextTrimming="CharacterEllipsis"` во все утилиты**: QRCodeGenerator добавил это в `FieldLabelStyle`. Распространить как улучшение.

7. **Тесты для `NormalizeColor` и `ValidateRenderOptions`**: Добавить тесты с некорректными цветами и размерами.

8. **XML doc comments**: Добавить документацию к `QRCodeService` public methods.

### Приоритет 4 — Не критично

9. **`Dispose()`CTS**: Убрать явный `Dispose()` — GC справится. Упрощает код.

10. **Размер окна**: Зафиксировать решение о `540x520` vs `480x440` как осознанное для многострочного ввода.
