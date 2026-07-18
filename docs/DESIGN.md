# AiteBar — Design System

## Обзор

AiteBar использует единую тёмную цветовую схему во всех окнах приложения. Основа — приглушённые тёмные тона с синим акцентом. Все окна (кроме MainWindow) наследуют `DarkWindow`, который через Win32 interop (`DwmSetWindowAttribute`) принудительно включает тёмный режим заголовка Windows.

---

## Цветовая палитра

### Глобальные ресурсы (App.xaml)

| Ключ | HEX | Назначение |
|------|-----|------------|
| `WindowBackground` | `#1A1A1C` | Фон окон настроек, диалогов |
| `PanelBackground` | `#F01F1F1F` | Фон панели (MainWindow), внутренних контейнеров |
| `BorderColor` | `#262A2A2A` | Внешняя рамка панели ( MainWindow, FileSorter, IconConverter, Timer) |
| `AccentColor` | `#007ACC` | Основной акцент (кнопки действия, чекбоксы, табы, hover-подсветка) |
| `MutedText` | `#9BA1A6` | Приглушённый текст (лейблы полей, подсказки) |
| `PrimaryText` | `#E3E3E3` | Основной текст |

### Формы и контролы (FormControlsResources.xaml)

| Ключ | HEX | Назначение |
|------|-----|------------|
| `FormControlBackground` | `#2D2D30` | Фон TextBox, ComboBox |
| `FormControlBackgroundHover` | `#343438` | Hover-фон контролов |
| `FormControlBorderBrush` | `#3A3A3E` | Рамка контролов по умолчанию |
| `FormControlBorderHoverBrush` | `#4A4A4E` | Hover-рамка контролов |
| `FormControlFocusBorderBrush` | `#3ABEFF` | Рамка при фокусе ( cyan-синий ) |
| `FormControlForegroundBrush` | `#FFFFFF` | Текст в контролах |
| `FormControlMutedForegroundBrush` | `#BFC3C8` | Стрелка ComboBox, приглушённый текст |
| `FormControlDisabledForegroundBrush` | `#777777` | Текст отключённых контролов |
| `FormControlErrorBorderBrush` | `#E85A5A` | Рамка ошибки |

### Контекстное меню (DarkContextMenu)

| Параметр | HEX |
|----------|-----|
| Фон | `#202020` |
| Рамка | `#242424` |
| Текст | `#EAEAEA` |
| Hover-подсветка | `#343434` |
| Gesture-текст | `#B8B8B8` |
| Отключённый текст | `#777777` |
| CornerRadius | `16` (скруглённый стиль) |
| Padding | `8` |

### ToolTip

| Параметр | HEX |
|----------|-----|
| Фон | `#202328` |
| Рамка | `#3A4148` |
| Текст | `#E7EAEE` |
| CornerRadius | `3` |
| Shadow | `Color=#000000, BlurRadius=8, Opacity=0.35` |

### Скроллбар (DarkScrollBarStyle)

| Параметр | HEX |
|----------|-----|
| Thumb (по умолчанию) | `#66676D`, Opacity `0.72` |
| Thumb (hover) | `#85868C`, Opacity `1` |
| CornerRadius | `3` |
| Ширина | `8` |

---

## Типографика

| Шрифт | Использование |
|-------|--------------|
| `Segoe UI` | Основной шрифт приложения (контекстные меню, QuickNote текст, TextBox) |
| `Segoe UI Variable Display` | Крупный цифровой дисплей (Timer/Stopwatch) |
| `FluentSystemIcons-Regular` | Иконки кнопок панели и утилит |
| `Font Awesome 7 Brands` | Брендовые иконки |
| `Material Icons` | Иконки контекстных меню (Cut/Copy/Paste) |
| `Segoe MDL2 Assets` | Системные иконки Windows (стрелки, пин, закрытие) |
| `Consolas` | Кодовые блоки в QuickNote |

### Размеры шрифтов

| Контекст | Размер | Вес |
|----------|--------|-----|
| Заголовок секции (Settings) | `16` | SemiBold |
| Подзаголовок | `11` | SemiBold |
| Лейбл поля | `11` | Normal |
| Основной текст меню | `14` | Normal |
| Кнопки панели (иконки) | `24` | Normal |
| QuickNote body | `14` | Normal |
| Timer display | `58` | Light |
| Compact timer | `23` | Regular |
| Footer/status текст | `11` | Normal |

---

## Окна приложения

### MainWindow (Панель быстрого доступа)

- **Тип**: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`
- **Поведение**: Topmost, скрыта из taskbar, появляется/скрывается по hotkey/край экрана
- **Внешний Border**: `Background=BorderColor`, `CornerRadius=8`, `BorderBrush=#24FFFFFF`, `BorderThickness=0.7`
  - **Тень**: `DropShadowEffect Color=#000000 BlurRadius=12 ShadowDepth=1 Opacity=0.32`
- **Внутренний Border**: `Background=PanelBackground`, `CornerRadius=7`
- **Drag Handle**: `Width=14`, `Height=44`, grip `Width=4 Height=18` цвет `#2A9CFF`
- **Кнопки панели**: `40×40`, `Margin=2,2`, `CornerRadius=4`
  - Hover: `Background=#25FFFFFF`
  - Кнопка "+" (BtnAdd): кружок `24×24` с `Background=#2A9CFF`, `CornerRadius=12`
    - Hover: `#1E6FD9`, Pressed: `#164C99`
  - Separator: `Width=1 Height=20`, `Fill=#332A9CFF`
  - Кнопка настроек (BtnAppSettings): `Foreground=#2A9CFF`
- **Focus Visual**: `BorderBrush=#2A9CFF`, `CornerRadius=4`

### SettingsWindow

- **Фон**: `#1A1A1C`
- **Размер**: `Width=880`, фиксированная высота
- **Структура**: Вкладки (TabControl) + форма редактирования кнопки + превью иконки/цвета
- **TabItem**:
  - Неактивная: `Foreground=MutedText`
  - Активная: `Foreground=White`, `BorderBrush=AccentColor` (нижняя полоса 2px)
  - Hover: `Foreground=White`
  - Focus: `BorderBrush=#3ABEFF`
- **Карточки** (CardStyle): `CornerRadius=8`, `Padding=14,12`, `BorderBrush=#3A3A3E`, `BorderThickness=1`
- **Иконка-превью**: `80×80`, `CornerRadius=12`, `Background=#1A1A1C`, `BorderBrush=#3A3A3E`
- **Разделитель**: `BorderBrush=#3A3A3E`, `BorderThickness=0,1,0,0`
- **Кнопки**: `ActionButtonStyle` (синяя `#007ACC`) и `SecondaryButtonStyle` (серая `#3E3E42`)
  - Hover secondary: `#4E4E52`
  - Disabled: `Background=#333335`, `Foreground=#666666`

### AppSettingsWindow

- **Фон**: `#1A1A1C`
- **Размер**: `720×560`
- **Структура**: 4 вкладки (General, Context Names, Hotkeys, Quick Tools)
- **HotkeyModifierButtonStyle** (ToggleButton):
  - `62×32`, `CornerRadius=6`
  - Неактивная: `Background=#151A22`, `BorderBrush=FormControlBorderBrush`
  - Hover: `Background=#1D2633`, `BorderBrush=FormControlFocusBorderBrush`
  - Checked: `Background=#2D7BEA`
  - Checked+Hover: `Background=#3487F3`
- **Badge-цвета для панелей**: `#2563EB`, `#059669`, `#D97706`, `#7C3AED`

### QuickNoteWindow

- **Тип**: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`
- **Размер**: `580×430`, `MinWidth=460`, `MinHeight=320`, ресайз через ручные grip-ы
- **Внешний Border**: `CornerRadius=14`, `Background=#202124`, `BorderBrush=#3A3B40`
  - **Тень**: `BlurRadius=16, ShadowDepth=3, Opacity=0.14`
- **Структура**: HeaderBar (52px) + Content (RichTextBox) + FooterBar (34px)
- **HeaderBar**: Прозрачный фон, содержит toolbar форматирования и кнопки управления
- **Toolbar-кнопки**: `32×32`, `CornerRadius=8`, `Foreground=#AFAFB7`
  - Hover: `Background=#303238`, `Foreground=#F4F4F6`
- **RichTextBox**: `Foreground=#F6F0E6`, `CaretBrush=#70B7FF`, `LineHeight=20`
- **Footer**: `Foreground=#74757A`, `FontSize=11`
- **Separator**: `Fill=#30FFFFFF`
- **Pin toggle**: Emoji 📌
- **Theme popup**: `CornerRadius=14`, `Background=#F8F8F8`, `BorderBrush=#DDDDDD`
- **Resize grips**: 8 направлений, прозрачные прямоугольники

### QuickNote Темы (QuickNoteTheme)

| ID | Background | Border | Text | MutedText | Accent | CodeBackground | CodeText | Link | Dark |
|----|-----------|--------|------|-----------|--------|----------------|----------|------|------|
| dark | `#202124` | `#3A3B40` | `#F6F0E6` | `#74757A` | `#70B7FF` | `#2A2C30` | `#E0E0E0` | `#70B7FF` | Yes |
| graphite | `#2A2B2E` | `#44464A` | `#F2EEE7` | `#8B8D92` | `#70B7FF` | `#34363A` | `#E0E0E0` | `#70B7FF` | Yes |
| rose | `#E9C7C3` | `#CFAEA9` | `#222222` | `#65514F` | `#0067B8` | `#D4B4AF` | `#1A1A1A` | `#0067B8` | No |
| clay | `#E5C6AE` | `#C9AA91` | `#222222` | `#63503F` | `#0067B8` | `#D0B399` | `#1A1A1A` | `#0067B8` | No |
| sand | `#E8D8B8` | `#CCBC9A` | `#222222` | `#625842` | `#0067B8` | `#D3C4A2` | `#1A1A1A` | `#0067B8` | No |
| lemon | `#E9E0B4` | `#CDC48F` | `#222222` | `#5F5A3E` | `#0067B8` | `#D4CB9E` | `#1A1A1A` | `#0067B8` | No |
| sage | `#C9DDC5` | `#ACC0A8` | `#222222` | `#4E604C` | `#0067B8` | `#B5D0B0` | `#1A1A1A` | `#0067B8` | No |
| mist | `#D7E1E5` | `#BAC5CA` | `#222222` | `#4F5E64` | `#0067B8` | `#C3CED2` | `#1A1A1A` | `#0067B8` | No |
| sky | `#C9DCEC` | `#AEC0D0` | `#222222` | `#4A5D6C` | `#0067B8` | `#B5CAD8` | `#1A1A1A` | `#0067B8` | No |
| lavender | `#D8CCE8` | `#BCB0CF` | `#222222` | `#584F68` | `#0067B8` | `#C4BAD4` | `#1A1A1A` | `#0067B8` | No |
| mauve | `#E4C8DD` | `#C8ACC1` | `#222222` | `#604F5B` | `#0067B8` | `#D0B4C9` | `#1A1A1A` | `#0067B8` | No |
| stone | `#D7D4CC` | `#BBB8B0` | `#222222` | `#5B5850` | `#0067B8` | `#C3C0B8` | `#1A1A1A` | `#0067B8` | No |

### TimerStopwatchWindow

- **Тип**: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`
- **Размер**: `420×420`, `MinWidth=420`, `MinHeight=330`
- **Цвета (собственная палитра)**:

| Ключ | HEX | Назначение |
|------|-----|------------|
| `TimerAccent` | `#007ACC` | Акцент |
| `TimerPanel` | `#0F1115` | Фон панели |
| `TimerSurface` | `#151922` | Фон поверхности контролов |
| `TimerSurfaceHover` | `#1B2330` | Hover фон |
| `TimerBorder` | `#16202A` | Внешняя рамка |
| `TimerControlBorder` | `#273344` | Рамка контролов |
| `TimerMuted` | `#9BA1A6` | Приглушённый текст |

- **Внешний Border**: `CornerRadius=8`, `Background=TimerBorder`, `BorderBrush=#332A9CFF`
  - **Тень**: `BlurRadius=14, ShadowDepth=2, Opacity=0.45`
- **Внутренний Border**: `CornerRadius=7`, `Background=TimerPanel`
- **Таймер дисплей**: `FontFamily=Segoe UI Variable Display`, `FontSize=58`, `FontWeight=Light`, `Foreground=#F2F4F7`
- **Compact mode**: `36px высота`, `CornerRadius=6/5`, дисплей `FontSize=23`
- **Mode toggle (RadioButton)**: `Height=32`, `CornerRadius=4`
  - Checked: `Background=TimerAccent`, `Foreground=White`
  - Hover: `Background=TimerSurfaceHover`
- **Preset buttons**: `Height=34`, `CornerRadius=4`, `Background=TimerSurface`, `BorderBrush=TimerControlBorder`

### FileSorterWindow

- **Тип**: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`
- **Размер**: `Width=320`, фиксированная высота
- **Внешний Border**: `CornerRadius=8`, `Background=BorderColor`, `BorderBrush=#332A9CFF`
  - **Тень**: `BlurRadius=14, ShadowDepth=2, Opacity=0.45`
- **Внутренний Border**: `CornerRadius=7`, `Background=PanelBackground`
- **Состояния**: Idle → Sorting (спиннер) → Completed (галочка)
  - Спиннер: `Ellipse Stroke=#1F3B56`, `Path Stroke=AccentColor`
  - Галочка: `Ellipse Fill=#143C27 Stroke=#16A34A`, `Checkmark=#22C55E`

### IconConverterWindow

- **Тип**: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`
- **Размер**: `680×540`, `MinWidth=640`, `MinHeight=500`, `ResizeMode=CanResizeWithGrip`
- **Внешний Border**: `CornerRadius=8`, `Background=BorderColor`, `BorderBrush=#332A9CFF`
  - **Тень**: `BlurRadius=14, ShadowDepth=2, Opacity=0.45`
- **Внутренний Border**: `CornerRadius=7`, `Background=PanelBackground`
- **Drop zone**: `Background=#151922`, `BorderBrush=#273344`, `CornerRadius=6`
- **Preview cells**: `Background=#101318`, `BorderBrush=#273344`, `CornerRadius=6`
- **OptionRadioButtonStyle**: `CornerRadius=4`,_Checked: `Background=AccentColor`, radio dot `16×16`, check `8×8`

### IconPickerWindow

- **Фон**: `#1E1E1E`
- **Размер**: `560×580`
- **Tab bar**: `Background=#252525`
  - Hover: `Background=#454545`
  - Focus: `BorderBrush=#3ABEFF`
- **Search box**: `Background=#2D2D2D`, `BorderBrush=#444444`, `Height=30`
- **Search hint**: `Foreground=#666666`, `FontSize=11`
- **Icon buttons**: `CornerRadius=6`, hover `#454545`, focus `BorderBrush=#3ABEFF`

### DarkDialog

- **Фон**: `#1E1E1E`
- **Размер**: `Width=420`, фиксированная высота
- **Кнопки**: `DialogBtnStyle`, `CornerRadius=4`, `Height=34`
  - Primary: `Background=#4285F4`
  - Secondary: `Background=#333333`
  - Hover: `Background=#555555`
  - Focus: `BorderBrush=#3ABEFF`

### Раздел «О программе»

- Находится последним пунктом навигации `AppSettingsWindow`; отдельное окно не используется.
- Наследует фон, типографику и ограничение ширины правой колонки окна настроек.
- Показывает иконку приложения, версию, разработчика, лицензию и стек.
- Действия ресурсов оформлены плоскими строками без контурных рамок; hover использует приглушенную заливку.

### RotationProfileSelectionWindow

- **Фон**: `#1A1A1C`
- **Размер**: `560×440`
- **Profile list**: `Background=#202022`, `BorderBrush=#3A3A3E`, `CornerRadius=6`
- **Selection count badge**: `Background=#2D2D30`, `BorderBrush=BorderColor`, `CornerRadius=4`

### TextPromptDialog

- **Фон**: `#1A1A1C`
- **Размер**: `Width=420`, фиксированная высота
- **Кнопки**: `ActionButtonStyle` + `SecondaryButtonStyle`, `Height=36`, `MinWidth=110`

---

## Стили кнопок

### ActionButtonStyle (Основная кнопка действия)

| Параметр | Значение |
|----------|----------|
| Background | `#007ACC` (AccentColor) |
| Foreground | `White` |
| Height | `36` |
| CornerRadius | `4` |
| FontSize | `12` |
| FontWeight | `SemiBold` |
| Hover | `#1C97EA` |
| Disabled | `Background=#333335`, `Foreground=#666666` |

### SecondaryButtonStyle

| Параметр | Значение |
|----------|----------|
| BasedOn | ActionButtonStyle |
| Background | `#3E3E42` |
| Hover | `#4E4E52` |

### HeaderButtonStyle (Кнопки заголовков окон)

| Параметр | Значение |
|----------|----------|
| Size | `32×32` |
| FontFamily | FluentSystemIcons-Regular |
| FontSize | `18` |
| Foreground | `#AEB6C1` |
| CornerRadius | `4` |
| Hover | `Background=#25FFFFFF`, `Foreground=White` |
| Focus | `BorderBrush=#3ABEFF` |

---

## Стили контролов

### TextBox (BaseTextBoxStyle)

| Состояние | Background | BorderBrush |
|-----------|-----------|-------------|
| Normal | `#2D2D30` | `#3A3A3E` |
| Hover | `#343438` | `#4A4A4E` |
| Focus | `#343438` | `#3ABEFF` |
| Disabled | `Opacity=0.55` | `Foreground=#777777` |

- `Height=32`, `Padding=8,0`, `CornerRadius=4`

### ComboBox (BaseComboBoxStyle)

- Аналогичные состояния TextBox
- Стрелка: `Fill=FormControlMutedForegroundBrush`, при hover/focus: `Fill=FormControlForegroundBrush`
- Выпадающий список: `CornerRadius=0,0,4,4`, `MaxHeight=300`
- ComboBoxItem: `Padding=10,7`, `CornerRadius=3`, Hover: `FormControlBackgroundHover`, Selected: `#3A3A3E`

### CheckBox (BaseCheckBoxStyle)

- Box: `16×16`, `CornerRadius=3`
- FocusFrame: `20×20`, `CornerRadius=4`
- Checkmark: `Stroke=White`, `StrokeThickness=2`
- Checked: `Background=AccentColor`, `BorderBrush=AccentColor`
- Hover: `Background=FormControlBackgroundHover`, `BorderBrush=FormControlBorderHoverBrush`
- Focus: `FocusFrame.BorderBrush=FormControlFocusBorderBrush`
- Варианты: `InlineCheckBoxStyle`, `DenseCheckBoxStyle`, `CompactCheckBoxStyle`, `CenteredCheckBoxStyle`, `SelectionListCheckBoxStyle`

---

## Анимации (Constants.cs)

| Константа | Значение | Назначение |
|-----------|----------|------------|
| `AnimationFadeMs` | `140` | Fade-in/out кнопок при drag-and-drop |
| `AnimationSlideMs` | `150` | Slide при перестановке кнопок |
| `PanelShowAnimationMs` | `175` | Появление панели |
| `PanelHideAnimationMs` | `140` | Скрытие панели |
| `QuickNoteSlideMs` | `200` | Анимация окна QuickNote |

---

## Скругления (CornerRadius)

| Элемент | Значение |
|---------|----------|
| Кнопки, TextBox, ComboBox, CheckBox | `4` |
| Карточки (CardStyle) | `8` |
| QuickNote окно | `14` |
| QuickNote toolbar-кнопки | `8` |
| Контекстное меню | `16` |
| ComboBox выпадающий список (низ) | `0,0,4,4` |
| Timer/Panel внешний Border | `8` |
| Timer/Panel внутренний Border | `7` |
| Timer compact | `6` / `5` |

---

## Тени (DropShadowEffect)

| Элемент | Color | BlurRadius | ShadowDepth | Opacity |
|---------|-------|-----------|-------------|---------|
| MainWindow panel | `#000000` | `12` | `1` | `0.32` |
| QuickNote | `#000000` | `16` | `3` (Direction=270) | `0.14` |
| FileSorter | `#000000` | `14` | `2` (Direction=270) | `0.45` |
| IconConverter | `#000000` | `14` | `2` (Direction=270) | `0.45` |
| Timer | `#000000` | `14` | `2` (Direction=270) | `0.45` |
| ToolTip | `#000000` | `8` | `1` | `0.35` |

---

## Границы панелей (Border)

Окна с `AllowsTransparency=True` (MainWindow, FileSorter, IconConverter, Timer) используют двухслойную структуру Border:

1. **Внешний**: `Background=BorderColor`, `BorderBrush=#332A9CFF` (полупрозрачный синий accent), `BorderThickness=0.7`
2. **Внутренний**: `Background=PanelBackground`, `CornerRadius` на 1 меньше внешнего

Это создаёт тонкий синий контур вокруг панели.

---

## Источники файлов

| Файл | Содержание |
|------|------------|
| `App.xaml` | Глобальные цвета, ContextMenu, ScrollBar, ToolTip, Separator стили |
| `FormControlsResources.xaml` | TextBox, ComboBox, CheckBox стили и ресурсы контролов |
| `SettingsResources.xaml` | CardStyle, SectionTitleStyle, SubsectionTitleStyle, FieldLabelStyle |
| `SettingsWindowResources.xaml` | ActionButtonStyle, SecondaryButtonStyle |
| `MainWindow.xaml` | Стили кнопок панели, DragHandle, layout |
| `QuickNoteWindow.xaml` | IconButtonStyle, FormatButtonStyle, QuickNoteScrollBarStyle |
| `QuickNoteTheme.cs` | Каталог тем QuickNote |
| `TimerStopwatchWindow.xaml` | Timer-специфичные стили и цвета |
| `Constants.cs` | Длительности анимаций |
| `DarkWindow.cs` | Базовый класс окон с тёмным заголовком Windows |
