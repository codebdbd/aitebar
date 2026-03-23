# Параметры интерфейса SmartScreenDock

Источник: `SmartScreenDock/MainWindow.xaml` (актуальное состояние).

## Окно панели

- `WindowStyle`: `None`
- `AllowsTransparency`: `True`
- `Background`: `Transparent`
- `Topmost`: `True`
- `ShowInTaskbar`: `False`
- `ResizeMode`: `NoResize`
- `SizeToContent`: `WidthAndHeight`
- `MinWidth`: `150`
- Начальная позиция: `Left=0`, `Top=-2000`

## Основной контейнер панели (`Border`)

- `Background`: `#F21C1C1C`
- `CornerRadius`: `8`
- `BorderBrush`: `#33FFFFFF`
- `BorderThickness`: `1`
- Внешний отступ (`Margin`): `5`

### Тень панели (`DropShadowEffect`)

- `BlurRadius`: `16`
- `Direction`: `270`
- `ShadowDepth`: `4`
- `Color`: `Black`
- `Opacity`: `0.4`

## Внутренний контейнер (`WrapPanel`)

- `Orientation`: `Horizontal`
- `HorizontalAlignment`: `Center`
- `VerticalAlignment`: `Center`
- `Margin`: `4,2`

## Общий стиль кнопок панели (`Style TargetType="Button"`)

- `Background`: `Transparent`
- `BorderThickness`: `0`
- `Foreground` (по умолчанию): `#E3E3E3`
- `FontFamily`: `Segoe Fluent Icons`
- `FontSize`: `20`
- `Width`: `40`
- `Height`: `40`
- `Margin`: `4,4`
- `Cursor`: `Hand`
- `FocusVisualStyle`: `{x:Null}`

### Шаблон кнопки (`ControlTemplate`)

- Фон в `Border`: `{TemplateBinding Background}`
- `CornerRadius`: `4`
- Контент выравнивается по центру:
- `HorizontalAlignment`: `Center`
- `VerticalAlignment`: `Center`

### Hover-состояние кнопки

- При `IsMouseOver=True`:
- `Background`: `#25FFFFFF`

## Цвета и состояния отдельных кнопок

- Базовые системные кнопки:
- `Foreground`: `#888888`
- Кнопка "Настройки / Добавить":
- `Foreground`: `#FFD700`
- Кнопка "Закрыть" при наведении:
- `Foreground`: `#E3544C`

## Разделители между блоками (`Rectangle`)

- `Width`: `1`
- `Height`: `20`
- `Fill`: `#40FFFFFF`
- `Margin`: `6,0`
- Часть разделителей динамически скрывается через `Visibility=Collapsed`

## Вычисляемые расстояния

- Горизонтальный промежуток между соседними кнопками: `8 px`
- Формируется как `4 px` справа у первой кнопки + `4 px` слева у следующей
- Вертикальные внешние отступы кнопки: `4 px` сверху и `4 px` снизу
