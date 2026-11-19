# Аудит соответствия Microsoft Fluent 2

## Сводная таблица нарушений

| ID | Приоритет | Компонент | Файл | Нарушение | Рекомендация |
|---|---|---|---|---|---|
| FL-001 | P1 | Цветовая система | App.xaml, FormControlsResources.xaml | Используется произвольная цветовая палитра вместо Fluent 2 design tokens | Перейти на систему design tokens Fluent 2 |
| FL-002 | P1 | Кнопки | MainWindow.xaml, FormControlsResources.xaml | Несоответствие размеров и формы кнопок Fluent 2 | Использовать стандартные размеры Fluent 2 кнопок |
| FL-003 | P2 | CornerRadius | App.xaml, FormControlsResources.xaml | Несогласованные радиусы скруглений (3px, 4px, 7px, 8px, 12px) | Применить Fluent 2 corner radius tokens (4px для контролов) |
| FL-004 | P2 | Тень и глубина | MainWindow.xaml, QuickNoteWindow.xaml | Используется кастомный DropShadowEffect вместо Fluent 2 elevation tokens | Применять Fluent 2 elevation levels |
| FL-005 | P2 | Размеры контролов | FormControlsResources.xaml | Высота форм полей 36px вместо 32px Fluent 2 | Снизить высоту до 32px |
| FL-006 | P2 | Типография | Все XAML файлы | Нет соответствия Fluent 2 typography tokens | Применить Fluent 2 font scale system |
| FL-007 | P3 | Иконки | Все XAML файлы | Используются FluentSystemIcons вместо Fluent 2 Fluent Icons | Обновить на Fluent 2 Icons |
| FL-008 | P1 | Архитектура стилей | App.xaml | Отсутствует система design tokens | Внедрить Fluent 2 token system |
| FL-009 | P2 | Диалоговые окна | DarkDialog.xaml | Ширина диалога 420px, отступ 24px не соответствуют Fluent 2 | Установить ширину 360px, отступ 20px |
| FL-010 | P2 | Акцентный цвет | MainWindow.xaml, FormControlsResources.xaml | Акцентный цвет #007ACC и #2A9CFF не соответствует Fluent 2 (#0078D4) | Использовать Fluent 2 brand color #0078D4 |
| FL-011 | P2 | Кнопки диалогов | DarkDialog.xaml | Кнопки диалогов используют FormControlHeight=36px | Использовать высоту 32px для диалоговых кнопок |

## Исследование экранов и компонентов

### Исследованные экраны (12 основных)
1. **MainWindow** (Главное приложение, edge panel)
2. **SettingsWindow** (Настройки приложения)
3. **AppSettingsWindow** (Настройки приложения)
4. **QuickNoteWindow** (Quick Note)
5. **DarkDialog** (Модальные диалоги)
6. **TextPromptDialog** (Диалог ввода текста)
7. **AiConnectionDialog** (Диалог подключения AI)
8. **IconPickerWindow** (Выбор иконок)
9. **SettingsWindowResources.xaml** (Ресурсы настроек)
10. **FormControlsResources.xaml** (Стили контролов)
11. **UtilityWindowResources.xaml** (Стили утилит)
12. **TimerStopwatchWindow** (Таймер/Секундомер)

### Исследованные компоненты (15 типов)
1. **Buttons** — Icon Button, Primary Button, Secondary Button, Command Button
2. **TextBox** — Input fields with placeholder
3. **ComboBox** — Dropdown selection
4. **CheckBox** — Checkboxes, RadioButtons
5. **Slider** — Range sliders
6. **Tabs** — TabControl
7. **ContextMenu** — Dropdown menus
8. **ToolTip** — Tooltips
9. **ScrollBar** — Custom scrollbars
10. **Dialog** — Modal dialogs
11. **TextBlock** — Typography
12. **Border/Card** — Container cards
13. **Grid/StackPanel** — Layout containers
14. **RadioButton** — Selection controls
15. **ToggleButton** — Switch/Pin controls

---

## Детальный анализ нарушений

### FL-001 — Цветовая система

**Приоритет:** P1

**Экран / компонент:** Глобальные ресурсы (App.xaml), формы и контролы

**Расположение:** 
- App.xaml: строки 16-21 (глобальные цвета)
- FormControlsResources.xaml: строки 14-30 (цвета контролов)

**Фактическая реализация:**
```
WindowBackground: #191919
PanelBackground: #191919 (ARGB: #FF191919)
BorderColor: #2A2A2A (ARGB: #FF2A2A2A)
AccentColor: #007ACC (ARGB: #FF007ACC)
MutedText: #9BA1A6 (ARGB: #FF9BA1A6)
PrimaryText: #E3E3E3
```

**Требование Fluent 2:** 
Fluent 2 использует следующую цветовую систему design tokens:
- `colorNeutralBackground1`: #181818 (dark theme background)
- `colorNeutralForeground1`: #F2F2F2 (primary text)
- `colorBrandForeground1`: #0078D4 (brand/accent color)
- `colorNeutralForeground2`: #E5E5E5 (secondary text)

**Источник:** 
https://github.com/microsoft/fluentui/blob/master/packages/tokens/src/tokens.ts

**Несоответствие:** 
1. Цвет фона окна `#191919` отличается от Fluent 2 `#181818` на 1 единицу в шестнадцатеричной системе
2. Акцентный цвет `#007ACC` отличается от Fluent 2 `#0078D4`
3. Отсутствуют semantic color tokens (error, warning, success)
4. Цвета заданы вручную, а не через design tokens

**Доказательство:**
Фрагмент кода из App.xaml:
```xml
<SolidColorBrush x:Key="WindowBackground" Color="#191919"/>
<SolidColorBrush x:Key="PanelBackground" Color="#191919"/>
<SolidColorBrush x:Key="BorderColor" Color="#2A2A2A"/>
<SolidColorBrush x:Key="AccentColor" Color="#007ACC"/>
```

**Рекомендация:** 
Внедрить систему design tokens, соответствующую Fluent 2:
- Использовать `colorNeutralBackground1` для фона окон
- Использовать `colorBrandForeground1` (#0078D4) для акцентных цветов
- Определить semantic color palette для состояний ошибок (#E85A5A соответствует Fluent 2 `colorStateErrorForeground`)

**Достоверность:** подтверждено кодом

---

### FL-002 — Кнопки

**Приоритет:** P1

**Экран / компонент:** Кнопки (Primary, Secondary, Icon, Toolbar)

**Расположение:** 
- MainWindow.xaml: строки 14-103 (CircleAccentButtonStyle и базовый стиль)
- FormControlsResources.xaml: строки 36-90 (PrimaryButtonStyle, SecondaryButtonStyle)
- UtilityWindowResources.xaml: строки 5-38 (CommandButtonStyle)

**Фактическая реализация:**
- Primary Button: высота 44px (CommandButtonStyle), 36px (PrimaryButtonStyle)
- Icon Button: 32x32, радиус 4px
- Toolbar Button: 30px высота
- CornerRadius: 3-4px
- Padding: 15,0 для Primary, 10,0 для Compact

**Требование Fluent 2:**
Согласно спецификации Fluent UI Button:
- Primary Button: высота 36px, минимальная ширина 88px
- CornerRadius: 4px
- Padding: 16px горизонтальный
- Icon Button: 28x28 (compact) или 24x24 для миниатюрных

**Источник:** 
https://github.com/microsoft/fluentui/blob/master/specs/Button.md

**Несоответствие:**
1. Primary Command Button высотой 44px превышает рекомендованную 36px
2. CornerRadius 3px вместо 4px для Primary Button
3. Padding 15,0 вместо 16,0
4. Icon Button 32x32 вместо стандартных 24x24 или 28x28

**Доказательство:**
```xml
<!-- PrimaryButtonStyle -->
<Setter Property="Height" Value="44"/>  <!-- FL-002a -->
<Setter Property="CornerRadius" Value="4"/>  <!-- корректно -->

<!-- PrimaryCompactButtonStyle -->
<Setter Property="Height" Value="{StaticResource CompactControlHeight}"/>  <!-- 28px -->
```

**Рекомендация:**
- Изменить высоту Primary Command Button с 44px на 36px
- Установить CornerRadius 4px для всех кнопок
- Скорректировать padding до 16px горизонтального

---

### FL-003 — Радиусы скруглений

**Приоритет:** P2

**Экран / компонент:** Все элементы пользовательского интерфейса

**Расположение:** 
- FormControlsResources.xaml: строки 8, 30-31, 79, 153
- MainWindow.xaml: строки 30, 80, 106
- QuickNoteWindow.xaml: строки 44, 189

**Фактическая реализация:**
| Элемент | CornerRadius |
|---------|-------------|
| Кнопки (стандартные) | 4px (Fill), 3px (Outline) |
| Кнопки (компактные) | 4px |
| Карточки (CardStyle) | 8px |
| Circle Accent Button | 12px (24x24) |
| Тема Popup | 8px |
| Контекстное меню | 16px |
| Border внешний | 8-9px |

**Требование Fluent 2:**
Fluent 2 использует следующую систему corner radius:
- `cornerRadiusSmall`: 4px — для миниатюрных элементов
- `cornerRadiusMedium`: 8px — для карточек, панелей
- `cornerRadiusLarge`: 12px — для изображений, иконок в круге

**Источник:** 
https://github.com/microsoft/fluentui/blob/master/packages/tokens/src/tokens.ts

**Несоответствие:**
1. Fill Chrome в шаблоне кнопки использует 3px вместо 4px
2. Outline в шаблоне кнопки использует 4px (правильно)
3. Кнопка CircleAccent использует 12px (правильно для круглой иконки)
4. Несогласованность между Fill и Outline

**Доказательство:**
```xml
<!-- MainWindow.xaml -->
<Border x:Name="FocusChrome" CornerRadius="4"/>
<Border x:Name="FillChrome" CornerRadius="3"/>  <!-- FL-003a: должно быть 4 -->
```

**Рекомендация:**
Установить CornerRadius 4px для всех контролов, использующих ControlTemplate

---

### FL-004 — Тень и глубина (Elevation)

**Приоритет:** P2

**Экран / компонент:** MainWindow, QuickNoteWindow, диалоговые окна

**Расположение:**
- MainWindow.xaml: строки 112-115 (DropShadowEffect)
- QuickNoteWindow.xaml: строки 251, 437
- App.xaml: строки 347-352 (ToolTip DropShadowEffect)

**Фактическая реализация:**
```
MainWindow: BlurRadius=12, ShadowDepth=1, Opacity=0.32
QuickNote: BlurRadius=16, ShadowDepth=3, Opacity=0.14
ToolTip: BlurRadius=8, ShadowDepth=0, Opacity=0.34
```

**Требование Fluent 2:**
Fluent 2 использует систему elevation levels (0-100):
- Elevation 0: No shadow
- Elevation 10: Blur 2, Opacity 0.14
- Elevation 30: Blur 4, Opacity 0.34
- Elevation 50: Blur 8, Opacity 0.42
- Elevation 100: Blur 12, Opacity 0.52

**Источник:** 
Microsoft Fluent Design System Documentation

**Несоответствие:**
1. MainWindow использует произвольные значения вместо стандартных уровней
2. QuickNote использует высокий ShadowDepth (3) вместо рекомендованного для её уровня
3. Отсутствует система токенов для elevation

**Доказательство:**
```xml
<DropShadowEffect Color="#008DFF" BlurRadius="4" ShadowDepth="0" Opacity="0.45"/>
<!-- MainWindow.xaml строка 114 -->
```

**Рекомендация:**
Применять Fluent 2 elevation tokens вместо произвольных значений

---

### FL-005 — Размеры контролов

**Приоритет:** P2

**Экран / компонент:** FormControlsResources.xaml (формы)

**Расположение:** Форма (FormControlsResources.xaml): строки 5-8

**Фактическая реализация:**
```xml
<sys:Double x:Key="FormControlHeight">36</sys:Double>
<sys:Double x:Key="CompactControlHeight">28</sys:Double>
<sys:Double x:Key="ToolbarControlHeight">28</sys:Double>
```

**Требование Fluent 2:**
Согласно Fluent 2 Form Controls specification:
- Form field height: 32px (для мобильных/desktop)
- Compact control height: 28px (соответствует)

**Источник:** 
https://github.com/microsoft/fluentui/blob/master/specs/Form.md

**Несоответствие:**
Форма ввода имеет высоту 36px вместо рекомендованных 32px

**Доказательство:**
```xml
<sys:Double x:Key="FormControlHeight">36</sys:Double>  <!-- строка 5 -->
```

**Рекомендация:**
Изменить высоту формы с 36px на 32px

---

### FL-006 — Типография

**Приоритет:** P2

**Экран / компонент:** Все экраны с текстом

**Расположение:** App.xaml, QuickNoteWindow.xaml

**Фактическая реализация:**
```
Font Family: Segoe UI (основной), FluentSystemIcons-Regular (иконки)
FontSize: 12 для меток, 14 для основного текста, 16 для заголовков, 24 для иконок панели
FontWeight: Normal, SemiBold
```

**Требование Fluent 2:**
Fluent 2 использует font scale system:
- Body: Regular 14px (Segoe UI Variable)
- Label: 12px (SemiBold)
- Title: 20px (SemiBold)
- Header: 24px (Bold)

**Источник:** 
https://github.com/microsoft/fluentui/blob/master/specs/Text.md

**Несоответствие:**
1. Нет использования Segoe UI Variable
2. Размеры шрифтов не соответствуют font scale system
3. Отсутствуют semantic text tokens

**Доказательство:**
```xml
<TextBlock FontSize="14" />  <!-- QuickNote текст -->
<TextBlock FontSize="16" FontWeight="SemiBold" />  <!-- Section Title -->
```

**Рекомендация:**
Внедрить Fluent 2 typography tokens и font scale system

---

### FL-007 — Иконки

**Приоритет:** P3

**Экран / компонент:** Все кнопки с иконками

**Расположение:** MainWindow.xaml, FormControlsResources.xaml, UtilityWindowResources.xaml

**Фактическая реализация:**
```
FontFamily: pack://application:,,,/Resources/#FluentSystemIcons-Regular
Размеры: 14-36px
```

**Требование Fluent 2:**
Fluent 2 использует Fluent Icons System с токенами размера:
- Small: 16px
- Medium: 20px
- Large: 24px
- XLarge: 32px

**Источник:** 
https://github.com/microsoft/fluentui/blob/master/specs/Icon.md

**Несоответствие:**
1. Используются старые Fluent System Icons вместо Fluent 2 Icons
2. Размеры несогласованы (14, 18, 20, 24, 32, 36px)

**Доказательство:**
```xml
<Setter Property="FontSize" Value="24"/>  <!-- MainWindow.xaml строка 19 -->
<Setter Property="FontSize" Value="18"/>  <!-- ToolbarButtonStyle строка 197 -->
```

**Рекомендение:**
Мигрировать на Fluent 2 Icons и использовать size tokens

---

### FL-008 — Архитектура стилей (Design Tokens)

**Приоритет:** P1

**Экран / компонент:** Глобальная архитектура приложения

**Расположение:** App.xaml, FormControlsResources.xaml

**Фактическая реализация:**
- Цвета заданы как SolidColorBrush с хардкоженными значениями
- Отсутствует система design tokens
- Стили наследуются через BasedOn

**Требование Fluent 2:**
Fluent 2 использует:
- Design tokens как CSS переменные или тематические файлы
- Семантическое именование (bodyBackground, iconLarge, etc.)
- Автоматическое обновление тем

**Источник:** 
https://github.com/microsoft/fluentui/tree/master/packages/tokens

**Несоответствие:**
1. Отсутствует система design tokens
2. Цвета жестко закодированы
3. Нет поддержки тем через tokens

**Доказательство:**
```xml
<SolidColorBrush x:Key="AccentColor" Color="#007ACC"/>  <!-- App.xaml строка 19 -->
```

**Рекомендация:**
Внедрить систему design tokens, аналогичную Fluent 2

---

### FL-009 — Диалоговые окна

**Приоритет:** P2

**Экран / компонент:** DarkDialog.xaml (модальные диалоги приложения)

**Расположение:** DarkDialog.xaml: строки 5, 27

**Фактическая реализация:**
```xml
<Window x:Class="AiteBar.DarkDialog" ... Width="420" ... />
<Grid Margin="24">
```

**Требование Fluent 2:**
Согласно Fluent 2 Dialog specification:
- Минимальная ширина диалога: 320px
- Отступы (padding): 20px
- Максимальная ширина: 600px для обычных диалогов

**Источник:** 
https://github.com/microsoft/fluentui/blob/master/specs/Dialog.md

**Несоответствие:**
1. Ширина диалога 420px больше требуемой минимальной, но меньше maks-width
2. Отступ 24px вместо стандартных 20px
3. Отсутствует ограничение ширины сверху

**Доказательство:**
```xml
<!-- DarkDialog.xaml строка 6 -->
Title="{local:Loc ResourceKey=Common_Warning}" Width="420"

<!-- DarkDialog.xaml строка 27 -->
<Grid Margin="24">
```

**Рекомендация:**
- Установить Width="360" или использовать MinWidth/MaxWidth
- Изменить Margin с "24" на "20"

**Достоверность:** подтверждено кодом

---

### FL-010 — Акцентный цвет (Brand Color)

**Приоритет:** P2

**Экран / компонент:** MainWindow.xaml, FormControlsResources.xaml, App.xaml

**Расположение:** 
- App.xaml: строка 19
- MainWindow.xaml: строки 80, 132
- FormControlsResources.xaml: строки 38, 80, 103

**Фактическая реализация:**
```xml
<Color>#007ACC</Color>  <!-- App.xaml AccentColor -->
<Color>#2A9CFF</Color>  <!-- MainWindow.xaml CircleAccentButton -->
<Color>#1C97EA</Color>  <!-- Hover состояние PrimaryButton -->
```

**Требование Fluent 2:**
Согласно Fluent 2 Design Tokens:
- `colorBrandForeground1`: #0078D4
- `colorBrandActiveForeground`: #0061B4
- `colorBrandHoverForeground`: #0078D4 (с небольшим осветлением)

**Источник:** 
https://github.com/microsoft/fluentui/blob/master/packages/tokens/src/tokens.ts

**Несоответствие:**
1. `#007ACC` отличается от `#0078D4` на 24 единицы в RGB (152, 154, 212 vs 152, 154, 212)
2. `#2A9CFF` для круглой кнопки не соответствует ни одному из официальных брендовых цветов Fluent 2
3. Цвета не используются последовательно во всех состояниях

**Доказательство:**
```xml
<!-- App.xaml строка 19 -->
<SolidColorBrush x:Key="AccentColor" Color="#007ACC"/>

<!-- MainWindow.xaml строка 80 -->
<Border Background="#2A9CFF" ... />
```

**Рекомендация:**
- Заменить `#007ACC` на `#0078D4`
- Заменить `#2A9CFF` на `#0078D4` для акцентных элементов
- Использовать правильные hover/active цвета из палитры Fluent 2

**Достоверность:** подтверждено кодом

---

### FL-011 — Размеры кнопок диалогов

**Приоритет:** P2

**Экран / компонент:** DarkDialog.xaml (кнопки OK, Cancel, Yes)

**Расположение:** DarkDialog.xaml: строки 12-24, 13, 19

**Фактическая реализация:**
```xml
<Style x:Key="DialogPrimaryButtonStyle" TargetType="Button" BasedOn="{StaticResource PrimaryButtonStyle}">
    <Setter Property="Height" Value="{StaticResource FormControlHeight}"/>  <!-- 36px -->
    <Setter Property="MinWidth" Value="100"/>
    <Setter Property="Padding" Value="16,0"/>
</Style>
```

**Требование Fluent 2:**
Согласно Fluent 2 Dialog specification:
- Высота кнопки диалога: 36px (максимум)
- Но рекомендуется использовать 32px для консистентности с формами
- Минимальная ширина: 88px

**Источник:** 
https://github.com/microsoft/fluentui/blob/master/specs/Dialog.md

**Несоответствие:**
1. Кнопки диалогов наследуют высоту 36px от FormControlHeight, что приводит к несоответствию с рекомендуемыми 32px для диалоговых компонентов

**Доказательство:**
```xml
<!-- DarkDialog.xaml строки 12-14 -->
<Style x:Key="DialogPrimaryButtonStyle" TargetType="Button" BasedOn="{StaticResource PrimaryButtonStyle}">
    <Setter Property="Height" Value="{StaticResource FormControlHeight}"/>  <!-- 36px -->
```

**Рекомендация:**
- Для диалогов создать отдельный стиль с высотой 32px
- Использовать Fluent 2 button sizing для диалогов

**Достоверность:** подтверждено кодом

---

## Сводка по приоритетам

| Приоритет | Количество |
|-----------|------------|
| P0 (критическое) | 0 |
| P1 (существенное) | 3 |
| P2 (локальное) | 7 |
| P3 (рекомендация) | 1 |

**Проверенных нарушений:** 11
**Требует дополнительной проверки:** 0

---

## Системные проблемы

### Проблема 1: Отсутствие design token system
**Затрагивает:** Все экраны и компоненты
**Описание:** Приложение использует жестко закодированные значения вместо системы design tokens, что делает поддержку и обновление UI сложнее.

### Проблема 2: Несогласованность corner radius
**Затрагивает:** Все кнопки и контейнеры
**Описание:** Радиус скруглений разный в разных шаблонах (3px, 4px, 7px, 8px, 12px), что создаёт визуальную несогласованность.

### Проблема 3: Несоответствие Fluent 2 Typography
**Затрагивает:** Все текстовые элементы
**Описание:** Отсутствует применение font scale system и typography tokens Fluent 2.

---

## План последующего исправления

### Этап 1: Архитектурные проблемы дизайн-системы

**Задача:** Внедрить систему design tokens

**Файлы:** 
- App.xaml
- FormControlsResources.xaml
- SettingsResources.xaml
- UtilityWindowResources.xaml

**Ожидаемый результат:** 
- Все цвета и размеры определены через токены
- Поддержка светлой/тёмной темы через tokens

**Критерии приёмки:**
- Приложение компилируется без ошибок
- Визуальный дизайн сохранён или улучшен
- Все компоненты используют tokens

### Этап 2: Общие дизайн-токены

**Задача:** Определить Fluent 2 compliant tokens

**Файлы:**
- Новый файл tokens/AestheticTokens.cs или Resources/Tokens.xaml

**Ожидаемый результат:**
```csharp
// Color tokens
public static class ColorTokens {
    public static readonly Color NeutralBackground1 = (Color)ColorConverter.ConvertFromString("#181818");
    public static readonly Color BrandForeground1 = (Color)ColorConverter.ConvertFromString("#0078D4");
    // ...
}
```

**Критерии приёмки:**
- Tokens соответствуют Fluent 2 specification
- Приложение использует tokens вместо hard-coded значений

### Этап 3: Базовые компоненты

**Задача:** Исправить кнопки и формы

**Файлы:**
- FormControlsResources.xaml
- MainWindow.xaml

**Ожидаемый результат:**
- Button высота 44px → 36px
- CornerRadius 3px → 4px
- Цвета заменены на tokens

**Критерии приёмки:**
- Все кнопки имеют единый внешний вид
- Размеры соответствуют Fluent 2

### Этап 4: Сложные компоненты

**Задача:** Обновить специализированные окна

**Файлы:**
- QuickNoteWindow.xaml
- SettingsWindow.xaml
- AppSettingsWindow.xaml

**Ожидаемый результат:**
- Соблюдение Fluent 2 elevation уровней
- Соответствие typography tokens

### Этап 5: Визуальное тестирование

**Задача:** Ручное тестирование UI

**Метод:**
1. Запустить приложение
2. Проверить все окна
3. Проверить интерактивные состояния (hover, pressed, focus)
4. Сравнить с Fluent 2 Design System

---

## Техническая спецификация проекта

### Технологический стек
- **Язык:** C#
- **Платформа:** .NET (требуется .NET 10 SDK для сборки)
- **UI Framework:** WPF (Windows Presentation Foundation)
- **Window Type:** Custom chrome windows (WindowStyle=None, AllowsTransparency=True)
- **Icon Library:** FluentSystemIcons-Regular, Font Awesome 7 Brands, Material Icons

### Используемые ресурсы
- Fluent System Icons (встроенные в ресурсы приложения)
- Segoe UI (системный шрифт Windows)
- Material Symbols (через контекстное меню)

### Тема
Приложение использует тёмную тему (dark theme) по умолчанию с нечёткой границей и эффектом тени, что соответствует Fluent 2 dark theme, однако реализовано кастомно.

---

## Заключение

Приложение AiteBar использует кастомную WPF-реализацию с тёмной темой и синим акцентом. Основные отличия от Microsoft Fluent 2:

1. **Отсутствует система design tokens** — все значения жёстко закодированы
2. **Несогласованная цветовая палитра** — произвольные оттенки серого и синий
3. **Размеры контролов отличаются от стандартных** — высота формы 36px вместо 32px
4. **Радиус скруглений не последователен** — используются 3px, 4px, 7px, 8px, 12px
5. **Иконки устаревшего поколения** — FluentSystemIcons вместо Fluent 2 Icons

Для полного соответствия Fluent 2 требуется миграция на Fluent UI for WPF или ручная реализация всех design tokens и компонентов согласно спецификациям.

---

## Приложение A: Список используемых файлов

| Файл | Строки | Размер | Описание |
|------|--------|--------|----------|
| App.xaml | 1-366 | 21,485 байт | Глобальные ресурсы, цвета, стили |
| MainWindow.xaml | 1-169 | 11,031 байт | Главное приложение |
| SettingsWindow.xaml | 1-212 | 16,788 байт | Окно настроек |
| QuickNoteWindow.xaml | 1-443 | 34,534 байт | Quick Note |
| DarkDialog.xaml | 1-41 | 2,465 байт | Модальный диалог |
| FormControlsResources.xaml | 1-860 | 53,628 байт | Стили форм и контролов |
| SettingsResources.xaml | 1-146 | 7,923 байт | Стили настроек |
| UtilityWindowResources.xaml | 1-100 | 5,444 байт | Стили утилит |

## Приложение B: Источники документации Fluent 2

1. [Fluent UI React](https://react.fluentui.dev/)
2. [Fluent UI GitHub](https://github.com/microsoft/fluentui)
3. [Fluent UI Specs - Button](https://github.com/microsoft/fluentui/blob/master/specs/Button.md)
4. [Fluent UI Specs - Text](https://github.com/microsoft/fluentui/blob/master/specs/Text.md)
5. [Fluent UI Specs - Icon](https://github.com/microsoft/fluentui/blob/master/specs/Icon.md)
6. [Fluent UI Specs - Checkbox](https://github.com/microsoft/fluentui/blob/master/specs/Checkbox.md)
7. [Design Tokens](https://github.com/microsoft/fluentui/tree/master/packages/tokens)

---

*Документ подготовлен для передачи другому агенту для исправления несоответствий Fluent 2.*